using System;
using UnityEngine;

namespace Deli.Setup
{
	/// <summary>
	///		A yield instruction that contains a result on completion.
	/// </summary>
	/// <typeparam name="TResult">The type of the result.</typeparam>
	public abstract class ResultYieldInstruction<TResult> : CustomYieldInstruction
	{
		/// <summary>
		///		The result of operation.
		/// </summary>
		/// <exception cref="InvalidOperationException">The operation has not been completed.</exception>
		public abstract TResult Result { get; }

		public ResultYieldInstruction<TResult> CallbackWith(Action<TResult> callback)
		{
			return new VoidCallback(this, callback);
		}

		public ResultYieldInstruction<TNext> CallbackWith<TNext>(Func<TResult, TNext> callback)
		{
			return new ResultCallback<TNext>(this, callback);
		}

		public ResultYieldInstruction<TResult> ContinueWith(Func<TResult, CustomYieldInstruction> continuation)
		{
			return new VoidContinuation(this, continuation);
		}

		public ResultYieldInstruction<TNext> ContinueWith<TNext>(Func<TResult, ResultYieldInstruction<TNext>> continuation)
		{
			return new ResultContinuation<TNext>(this, continuation);
		}

		private class VoidCallback : ResultYieldInstruction<TResult>
		{
			private readonly ResultYieldInstruction<TResult> _inst;

			private Action<TResult>? _callback;

			public override bool keepWaiting
			{
				get
				{
					if (!_inst.keepWaiting)
					{
						if (_callback is not null)
						{
							_callback(_inst.Result);
							_callback = null;
						}

						return false;
					}

					return true;
				}
			}

			public override TResult Result => _inst.Result;

			public VoidCallback(ResultYieldInstruction<TResult> inst, Action<TResult> callback)
			{
				_inst = inst;
				_callback = callback;
			}
		}

		private class ResultCallback<TNext> : ResultYieldInstruction<TNext>
		{
			private readonly ResultYieldInstruction<TResult> _inst;
			private readonly Func<TResult, TNext> _callback;

			public override bool keepWaiting => _inst.keepWaiting;

			private TNext? _result;
			public override TNext Result => _result ??= _callback(_inst.Result);

			public ResultCallback(ResultYieldInstruction<TResult> inst, Func<TResult, TNext> callback)
			{
				_inst = inst;
				_callback = callback;
			}
		}

		private class VoidContinuation : ResultYieldInstruction<TResult>
		{
			private readonly ResultYieldInstruction<TResult> _inst;
			private readonly Func<TResult, CustomYieldInstruction> _contFactory;

			private CustomYieldInstruction? _cont;

			public override bool keepWaiting
			{
				get
				{
					if (_cont is null)
					{
						if (_inst.keepWaiting)
						{
							return true;
						}

						_cont = _contFactory(_inst.Result);
					}

					return _cont.keepWaiting;
				}
			}

			public override TResult Result => _inst.Result;

			public VoidContinuation(ResultYieldInstruction<TResult> inst, Func<TResult, CustomYieldInstruction> contFactory)
			{
				_inst = inst;
				_contFactory = contFactory;
			}
		}

		private class ResultContinuation<TNext> : ResultYieldInstruction<TNext>
		{
			private readonly ResultYieldInstruction<TResult> _inst;
			private readonly Func<TResult, ResultYieldInstruction<TNext>> _contFactory;

			private ResultYieldInstruction<TNext>? _cont;

			public override bool keepWaiting
			{
				get
				{
					if (_cont is null)
					{
						if (_inst.keepWaiting)
						{
							return true;
						}

						_cont = _contFactory(_inst.Result);
					}

					return _cont.keepWaiting;
				}
			}

			public override TNext Result
			{
				get
				{
					if (_cont is null)
					{
						throw new InvalidOperationException("This instruction is not finished.");
					}

					return _cont.Result;
				}
			}

			public ResultContinuation(ResultYieldInstruction<TResult> inst, Func<TResult, ResultYieldInstruction<TNext>> contFactory)
			{
				_inst = inst;
				_contFactory = contFactory;
			}
		}
	}
}