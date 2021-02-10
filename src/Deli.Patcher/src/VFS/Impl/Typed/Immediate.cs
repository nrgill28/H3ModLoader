using Deli.Immediate;

namespace Deli.VFS
{
	public class ImmediateTypedFileHandle<T> : TypedFileHandle<ImmediateReader<T>, T> where T : notnull
	{
		public ImmediateTypedFileHandle(IFileHandle handle, ImmediateReader<T> reader) : base(handle, reader)
		{
		}

		protected override T Read()
		{
			return Reader(this);
		}
	}
}
