using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace osp
{
    public enum FlexOrientation
    {
        Row, // 从左到右
        Column, // 从上到下
    }
    public enum FlexWrap
    {
        NoWrap, // 不换行
        Wrap, // 换行
    }
    // 提供一种类似于 web flex 的弹性布局控件
    public class FlexPanel : Panel
    {
        public FlexOrientation Orientation
        {
            get { return (FlexOrientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Orientation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(FlexOrientation), typeof(FlexPanel), new PropertyMetadata(FlexOrientation.Row));


        public FlexWrap Wrapping
        {
            get { return (FlexWrap)GetValue(WrappingProperty); }
            set { SetValue(WrappingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Wrapping.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WrappingProperty =
            DependencyProperty.Register("Wrapping", typeof(FlexWrap), typeof(FlexPanel), new PropertyMetadata(FlexWrap.Wrap));

        public Thickness Spacing
        {
            get { return (Thickness)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Spacing.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register("Spacing", typeof(Thickness), typeof(FlexPanel), new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        private bool Wraps()
        {
            return Wrapping == FlexWrap.Wrap;
        }
        private bool RowDir()
        {
            return Orientation == FlexOrientation.Row;
        }
        private bool Reverse()
        {
            return false;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var colsum = 0.0;
            var rowsum = 0.0;
            var colmax = 0.0;
            var rowmax = 0.0;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];
                var desired = child.DesiredSize;
                (double Width, double Height) desiredspaced = (desired.Width + Spacing.Left + Spacing.Right, desired.Height + Spacing.Top + Spacing.Bottom);
                if (RowDir())
                {
                    if (colsum + desiredspaced.Width > finalSize.Width && Wraps()) // 换行
                    {
                        rowsum += rowmax;
                        rowmax = 0;
                        colsum = 0;
                    }
                    if (!Wraps() || colsum < finalSize.Width && rowsum < finalSize.Height)
                        child.Arrange(new Rect(colsum, rowsum, desiredspaced.Width, desiredspaced.Height));
                    colsum += desiredspaced.Width;
                    rowmax = Math.Max(rowmax, desired.Height);
                }
                else
                {
                    if (rowsum + desiredspaced.Height > finalSize.Height && Wraps()) // 换行
                    {
                        colsum += colmax;
                        colmax = 0;
                        rowsum = 0;
                    }
                    if (!Wraps() || colsum < finalSize.Width && rowsum < finalSize.Height)
                        child.Arrange(new Rect(colsum, rowsum, desiredspaced.Width, desiredspaced.Height));
                    rowsum += desiredspaced.Height;
                    colmax = Math.Max(colmax, desired.Width);
                }
            }
            return finalSize;
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            var colsum = 0.0;
            var rowsum = 0.0;
            var colmax = 0.0;
            var rowmax = 0.0;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                var child = InternalChildren[i];
                child.Measure(availableSize);
                var desired = child.DesiredSize;
                (double Width, double Height) desiredspaced = (desired.Width + Spacing.Left + Spacing.Right, desired.Height + Spacing.Top + Spacing.Bottom);
                if (RowDir())
                {
                    if (colsum + desiredspaced.Width > availableSize.Width && Wraps()) // 换行
                    {
                        colmax = Math.Max(colmax, colsum);
                        rowsum += rowmax;
                        rowmax = 0;
                        colsum = 0;
                    }
                    colsum += desiredspaced.Width;
                    rowmax = Math.Max(rowmax, desired.Height);
                }
                else
                {
                    if (rowsum + desiredspaced.Height > availableSize.Height && Wraps()) // 换行
                    {
                        rowsum = Math.Max(rowmax, rowsum);
                        colsum += colmax;
                        colmax = 0;
                        rowsum = 0;
                    }
                    rowsum += desiredspaced.Height;
                    colmax = Math.Max(colmax, desired.Width);
                }
            }
            if (RowDir())
            {
                colmax = Math.Max(colmax, colsum); // wraps last
                rowsum += rowmax;
                rowmax = 0;
                colsum = 0;
                return new Size(colsum, rowsum);
            }
            else
            {
                rowsum = Math.Max(rowmax, rowsum);
                colsum += colmax;
                colmax = 0;
                rowsum = 0;
                return new Size(colsum, rowsum);
            }
        }

    }
}
