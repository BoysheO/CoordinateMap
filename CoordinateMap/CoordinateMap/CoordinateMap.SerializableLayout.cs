using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;

namespace com.susi.toolkit
{
    ///<summary>

    /// </summary>
    public partial class CoordinateMap<T> : CoordinateMap<T>.ISerializableDefineData,CoordinateMap<T>.ISerializableData
    {
        public interface ISerializableDefineData
        {
            int GXo { get; }
            int GYo { get; }
            int GXmin { get; }
            int GYmin { get; }
            int Width { get; }
            int Height { get; }
        }
        public interface ISerializableData
        {
            int GXo { get; }
            int GYo { get; }
            int GXmin { get; }
            int GYmin { get; }
            int Width { get; }
            int Height { get; }
            T[] SerializableData { get; }
        }

    }
}