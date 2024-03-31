using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AGF2BMP2AGF
{
	public static class Operation
	{
		public static byte[] StructToBytes<T>(T str) where T : struct
		{
			int size = Marshal.SizeOf(str);
			byte[] arr = new byte[size];
			var ptr = Marshal.AllocHGlobal(size);
			Marshal.StructureToPtr(str, ptr, true);
			Marshal.Copy(ptr, arr, 0, size);
			Marshal.FreeHGlobal(ptr);
			return arr;
		}

		public static T ByteArrayToStructure<T>(byte[] bytes, int offset = 0) where T : struct
		{
				int size = Marshal.SizeOf(typeof(T));
				var ptr = Marshal.AllocHGlobal(size);
				Marshal.Copy(bytes, offset, ptr, size);
				var str = Marshal.PtrToStructure<T>(ptr);
				Marshal.FreeHGlobal(ptr);
				return str;
		}
		
		public static T[] ByteArrayToStructureArray<T>(byte[] bytes, int offset, int length) where T : struct
		{
			int traveled = 0;
			var list = new List<T>();
			int size = Marshal.SizeOf(typeof(T));
			while (traveled < length)
			{
				var ptr = Marshal.AllocHGlobal(size);
				Marshal.Copy(bytes, offset + traveled, ptr, size);
				var str = Marshal.PtrToStructure<T>(ptr);
				Marshal.FreeHGlobal(ptr);
				list.Add(str);
				traveled += size;
			}
			return list.ToArray();
		}
    }
}
