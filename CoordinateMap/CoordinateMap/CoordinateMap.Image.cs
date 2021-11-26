using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Toolkit.HighPerformance;
namespace com.boysheo.toolkit
{
    partial class CoordinateMap<T>
    {
        /// <summary>
        /// 输出2D布局的数组
        /// </summary>
        public Span2D<T> AsSpan2D=>new Span2D<T>(Data.Array,Height,Width);

        /// <summary>
        /// 输出1D布局数组
        /// </summary>
        public Span<T> AsSpan => Data.AsSpan();
    }
}
