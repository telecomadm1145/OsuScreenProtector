using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace osp
{
    public class StackPanel : Panel
    {


        public Thickness Spacing
        {
            get { return (Thickness)GetValue(SpacingProperty); }
            set { SetValue(SpacingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Spacing.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register("Spacing", typeof(Thickness), typeof(StackPanel), new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));


        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Orientation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(Orientation), typeof(StackPanel), new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));


        protected override Size ArrangeOverride(Size arrangeSize)
        {
            Rect finalRect = new Rect(arrangeSize);
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement control = InternalChildren[i];
                var desired = control.DesiredSize;
                finalRect.Size = desired;
                if (Orientation == Orientation.Horizontal)
                {
                    finalRect.Height = Math.Max(desired.Height, arrangeSize.Height);
                }
                else
                {
                    finalRect.Width = Math.Max(desired.Width, arrangeSize.Width);
                }
                control.Arrange(finalRect);
                if (Orientation == Orientation.Horizontal)
                {
                    finalRect.X += Spacing.Left;
                    finalRect.X += desired.Width;
                    finalRect.X += Spacing.Right;
                }
                else
                {
                    finalRect.Y += Spacing.Top;
                    finalRect.Y += desired.Height;
                    finalRect.Y += Spacing.Bottom;
                }
            }
            return arrangeSize;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            (double Width,double Height) finalsize = default;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement control = InternalChildren[i];
                control.Measure(availableSize);
                var desired = control.DesiredSize;
                if (Orientation == Orientation.Horizontal)
                {
                    finalsize.Width += Spacing.Left;
                    finalsize.Width += desired.Width;
                    finalsize.Width += Spacing.Right;
                    finalsize.Height = Math.Max(desired.Height,finalsize.Height);
                }
                else
                {
                    finalsize.Height += Spacing.Top;
                    finalsize.Height += desired.Height;
                    finalsize.Height += Spacing.Bottom;
                    finalsize.Width = Math.Max(desired.Width,finalsize.Width);
                }
            }
            if (Orientation == Orientation.Horizontal)
            {
                finalsize.Height += Spacing.Top;
                finalsize.Height += Spacing.Bottom;
            }
            else
            {
                finalsize.Width += Spacing.Left;
                finalsize.Width += Spacing.Right;
            }
            return new Size(finalsize.Width >0?finalsize.Width:0,finalsize.Height > 0?finalsize.Height:0);
        }
    }
}
