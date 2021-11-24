using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace com.boysheo.toolkit
{
    /// <summary>
    /// 有限域的稠密图
    /// 这个数据结构描述了一个绘制在世界坐标系中的2D地图，并且地图本地也定义了一个本地坐标系。
    /// 本数据结构主要处理世界坐标、本地坐标、他地坐标的互相转换和处理地图内包含大量元素的地图数据
    /// 如无特殊说明，坐标是本地坐标
    /// 序列化指南：
    /// 可以按<see cref="CoordinateMap{T}"/>序列化以兼容序列化器；
    /// 可以按<see cref="CoordinateMap{T}.ISerializableData"/>以最少条目序列化；
    /// 按<see cref="ISerializableDefineData"/>接口序列化，可以获得定义信息；
    /// 线程安全：否
    /// </summary>
    public partial class CoordinateMap<T> :  IDisposable
    {
        #region structure

        public readonly struct Entry
        {
            public void Deconstruct(out int x, out int y, out T value)
            {
                x = X;
                y = Y;
                value = Value;
            }

            public void Deconstruct(out int x, out int y)
            {
                x = X;
                y = Y;
            }

            public readonly CoordinateMap<T> Map;
            public readonly int X;
            public readonly int Y;

            public Entry(CoordinateMap<T> map, int x, int y)
            {
                Map = map ?? throw new ArgumentNullException(nameof(map));
                X = x;
                Y = y;
            }

            public T Value
            {
                get => Map.Get(X, Y);
                set => Map.Set(X, Y, value);
            }
        }

        #endregion

        /// <summary>
        /// 本地原点的世界坐标
        /// </summary>
        public int GXo { get; private set; }

        /// <summary>
        /// 本地原点的世界坐标
        /// </summary>
        public int GYo { get; private set; }

        /// <summary>
        /// 世界坐标
        /// </summary>
        public int GXmin { get; private set; }

        /// <summary>
        /// 世界坐标
        /// </summary>
        public int GXmax => GXmin + Width;

        /// <summary>
        /// 世界坐标
        /// </summary>
        public int GYmin { get; private set; }

        /// <summary>
        /// 世界坐标
        /// </summary>
        public int GYmax => GYmin + Height;

        /// <summary>
        /// x轴跨度
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// y轴跨度
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// 本地坐标
        /// </summary>
        public int Xmin => GXmin - GXo;

        /// <summary>
        /// 本地坐标
        /// </summary>
        public int Ymin => GYmin - GYo;

        /// <summary>
        /// 本地坐标
        /// </summary>
        public int Xmax => Xmin + Width;

        /// <summary>
        /// 本地坐标
        /// </summary>
        public int Ymax => Ymin + Height;

        /// <summary>
        /// 数据容量
        /// </summary>
        public int Capacity => Width * Height;
        
        /// <summary>
        /// 仅用于序列化。不会校验设置到类型的数据可靠性
        /// 重要：保留此值引用会导致不可预料的问题，影响ArrayPool的正常工作。不应该在外部持有此值引用
        /// </summary>
        [Obsolete("only for serialize",true)]
        public T[] SerializableData
        {
            get
            {
                // ReSharper disable once PossibleNullReferenceException
                if (Data.Array.Length == Data.Count) return Data.Array;
                var copy = Data.AsSpan().ToArray();
                ArrayPool<T>.Shared.Return(Data.Array);
                Data = new ArraySegment<T>(copy);
                return copy;
            }
            set
            {
                if (Data.Array != null)
                {
                    ArrayPool<T>.Shared.Return(Data.Array);
                }

                Data = new ArraySegment<T>(value);
            }
        }
        
        private ArraySegment<T> Data;

        public T this[int x, int y]
        {
            get => Get(x, y);
            set => Set(x, y, value);
        }
        /// <summary>
        /// 仅用于兼容序列化
        /// </summary>
        [Obsolete("only for serialize", true)]
        public CoordinateMap(){}

        /// <summary>
        /// 如地图较大，请使用<see cref="BuildAsync"/>
        /// </summary>
        /// <param name="gxmin">地图在绝对坐标系的的左下角坐标</param>
        /// <param name="gymin">地图在绝对坐标系的左下角坐标</param>
        /// <param name="gx_locO">地图在绝对坐标系的0点坐标</param>
        /// <param name="gy_locO">地图在绝对坐标系的0点坐标</param>
        /// <param name="width">地图宽度</param>
        /// <param name="height">地图高度</param>
        /// <param name="defaultElement">初始化时使用哪个默认值</param>
        /// <exception cref="ArgumentOutOfRangeException">没有满足width height ∈[1,+∞)</exception>
        /// <exception cref="OverflowException">width*height超出int范围</exception>
        /// <exception cref="NotSupportedException">当泛型是引用类型，default不可以是null以外的值</exception>
        public CoordinateMap(int gxmin, int gymin, int gx_locO, int gy_locO, int width, int height,
            T defaultElement = default)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException($"wh({width},{height}) should ∈[1,n)");
            try
            {
                var res = checked(width * height);
            }
            catch
            {
                throw new OverflowException($"this map is too large.wh({width},{height})");
            }

            if (typeof(T).IsClass && defaultElement != null)
            {
                throw new NotSupportedException($"only null is valid when T is class");
            }

            GXmin = gxmin;
            GYmin = gymin;
            Width = width;
            Height = height;
            GXo = gx_locO;
            GYo = gy_locO;
            Data = new ArraySegment<T>(ArrayPool<T>.Shared.Rent(Capacity), 0, Capacity);
            Data.AsSpan().Fill(defaultElement);
        }
        
        public bool IsOutOfRange(int x, int y)
        {
            return x < Xmin || x >= Xmin + Width || y < Ymin || y >= Ymin + Height;
        }

        /// <summary>
        /// 世界坐标投影到本地
        /// </summary>
        public (int x, int y) GXYToXY(int gx, int gy)
        {
            var x = gx - GXo;
            var y = gy - GYo;
            return (x, y);
        }

        /// <summary>
        /// 本地坐标转投影到世界
        /// </summary>
        public (int gx, int gy) XYToGXY(int x, int y)
        {
            var gx = x + GXo;
            var gy = y + GYo;
            return (gx, gy);
        }


        /// <summary>本地xy投影到内部一维数组的坐标</summary>
        /// <exception cref="OverflowException">计算所得的结果大于int上限</exception>
        private int XYToIndex(int x, int y)
        {
            return checked(x - Xmin + (y - Ymin) * Width);
        }

        /// <exception cref="ArgumentOutOfRangeException">xy超范围</exception>
        public T Get(int x, int y)
        {
            if (IsOutOfRange(x, y)) throw new ArgumentOutOfRangeException($"xy({x},{y})");
            var idx = XYToIndex(x, y);
            return Data.AsSpan()[idx];
        }

        /// <exception cref="ArgumentOutOfRangeException">xy超范围</exception>
        public void Set(int x, int y, T value)
        {
            if (IsOutOfRange(x, y)) throw new ArgumentOutOfRangeException($"xy({x},{y})");
            var idx = XYToIndex(x, y);
            Data.AsSpan()[idx] = value;
        }

        /// <summary>
        /// 重新定义地图定义域、坐标，保持元素不变，元素对应的世界坐标不变。参数意义和限制参见构造函数
        /// 注意，如果重新定义的定义域与原来的定义域不同，则抛弃超出本地坐标的元素
        /// 懒得优化，会占用双倍内存；会卡
        /// </summary>
        public void Resize(int gxmin, int gymin, int gx_locO, int gy_locO,
            int width, int height,
            T defaultElement = default)
        {
            var map = new CoordinateMap<T>(gxmin, gymin, gx_locO, gy_locO, width, height, defaultElement);
            //将旧数据复制到新图中
            foreach (var entry in this.AsEnumerable()
                .Select<Entry, (int x, int y, T value)>(entry =>
                {
                    var (gx, gy) = XYToGXY(entry.X, entry.Y);
                    var (x, y) = map.GXYToXY(gx, gy);
                    return (x, y, entry.Value);
                })
                .Where(entry => !map.IsOutOfRange(entry.x, entry.y)))
            {
                map.Set(entry.x, entry.y, entry.value);
            }

            //copy state to this
            if (Data.Array != null)
            {
                ArrayPool<T>.Shared.Return(Data.Array);
            }
            Height = map.Height;
            Width = map.Width;
            GXmin = map.GXmin;
            GYmin = map.GYmin;
            GXo = map.GXo;
            GYo = map.GYo;
            Data = map.Data;
        }

        public IEnumerable<Entry> AsEnumerable()
        {
            for (int x = Xmin; x < Xmin + Width; x++)
            {
                for (int y = Ymin; y < Ymin + Height; y++)
                {
                    yield return new Entry(this, x, y);
                }
            }
        }

        /// <summary>
        /// 即使不调用，也不会内存泄露;但是会让内存复用效果减少
        /// Dispose之后不再保证本对象正常运行。
        /// </summary>
        public void Dispose()
        {
            if (Data.Array != null)
            {
                ArrayPool<T>.Shared.Return(Data.Array);
                Data = default;
            }
        }
    }
}