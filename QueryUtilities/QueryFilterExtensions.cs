using System.Linq.Expressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace Zhally.Toolkit.QueryUtilities;

public static class QueryFilterExtensions
{
    /// <summary>
    /// 流式筛选IQueryable数据，平衡性能和内存占用
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="query">原始查询</param>
    /// <param name="productionFilter">数据库端筛选表达式（生产阶段）</param>
    /// <param name="consumptionFilter">内存端筛选委托（消费阶段）</param>
    /// <param name="batchSize">批次大小</param>
    /// <param name="maxResults">最大结果数量（达到后提前终止）</param>
    /// <returns>筛选后的结果列表</returns>
    public static async Task<List<T>> FilterWithChannelAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> productionFilter,
        Func<T, bool> consumptionFilter,
        int batchSize, int? maxResults,
        CancellationToken token)
        where T : class
    {
        // 创建有界通道控制内存占用
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(2 * batchSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

        var result = new List<T>();
        bool stopProcessing = false;

        // 消费者任务：处理并筛选数据
        async Task ComsumerAsync(CancellationToken token)
        {
            await foreach (var item in channel.Reader.ReadAllAsync(token))
            {
                // 检查是否已达到最大结果数
                if (maxResults.HasValue && result.Count >= maxResults.Value)
                {
                    stopProcessing = true;
                    break;
                }

                // 应用内存筛选条件
                if (consumptionFilter(item))
                {
                    result.Add(item);
                }
            }
        }

        // 生产者任务：从数据库分批读取数据
        async Task ProducerAsync(CancellationToken token)
        {
            try
            {
                var page = 0;
                while (!stopProcessing)
                {
                    // 应用数据库筛选并分页查询
                    var batch = await query
                        .Where(productionFilter)
                        .Skip(page * batchSize)
                        .Take(batchSize)
                        .ToListAsync(token);

                    if (batch.Count == 0)
                        break; // 没有更多数据

                    // 将批次数据写入通道
                    foreach (var item in batch)
                    {
                        if (stopProcessing) break;
                        await channel.Writer.WriteAsync(item, token);
                    }

                    page++;
                }
            }
            finally
            {
                channel.Writer.Complete(); // 通知消费者数据已写完
            }
        }

        var consumerTask = ComsumerAsync(token);
        var producerTask = ProducerAsync(token);

        // 等待所有任务完成
        await Task.WhenAll(producerTask, consumerTask);

        return result;
    }
}
