using System;
using System.Threading.Tasks;

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
}
