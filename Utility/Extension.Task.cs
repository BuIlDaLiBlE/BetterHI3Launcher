using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

#nullable enable
namespace BetterHI3Launcher.Utility;

public static partial class Extension
{
	public static T WaitResult<T>(this Task<T> task)
	{
		if (task.IsCompleted)
		{
			return task.Result;
		}

		Exception? baseException = task.Exception?.Flatten().InnerException;
		if (task.IsCanceled)
		{
			throw baseException ?? new TaskCanceledException();
		}

		if (task.IsFaulted)
		{
			throw baseException ?? new Exception("Unknown error has occurred");
		}

		return task.GetAwaiter().GetResult();
	}

	public static async Task ParallelForeachAsync<T>(
		this IEnumerable<T>                   source,
		Func<T, CancellationToken, ValueTask> consumer,
		int                                   maxDegreeOfParallelism = -1,
		CancellationToken                     token                  = default)
	{
		if (maxDegreeOfParallelism <= 0)
		{
			maxDegreeOfParallelism = Environment.ProcessorCount;
		}

		var actionBlock = new ActionBlock<(T, CancellationToken)>(async item => await consumer(item.Item1, item.Item2),
                                                                  new ExecutionDataflowBlockOptions
                                                                  {
                                                                      MaxDegreeOfParallelism = maxDegreeOfParallelism,
                                                                      CancellationToken = token,
                                                                      BoundedCapacity = maxDegreeOfParallelism * 2
                                                                  });

        try
		{
			foreach (T item in source)
			{
				token.ThrowIfCancellationRequested();
				if (!await actionBlock.SendAsync((item, token), token))
				{
					break;
				}
			}
		}
		catch (Exception ex)
		{
			((IDataflowBlock)actionBlock).Fault(ex);
			throw;
		}

		actionBlock.Complete();
		await actionBlock.Completion;
	}
}
