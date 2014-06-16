using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;
using Coding4Fun.Kinect.Wpf;
using Dynamo.Controls;
using Dynamo.Models;
using Dynamo.Nodes;
using Dynamo.UI;
using Microsoft.Research.Kinect.Nui;
using ProtoCore.AST.AssociativeAST;
using Point = Autodesk.DesignScript.Geometry.Point;

namespace DynamoKinect
{
    [NodeName("Kinect")]
    [NodeCategory(BuiltinNodeCategories.IO_HARDWARE)]
    [NodeDescription("Read depth and hand position data from a kinect.")]
    [IsDesignScriptCompatible]
    public class Kinect : NodeModel, IWpfNode
    {
        private Runtime runtime;
        private Image image1;
        public override bool ForceReExecuteOfNode
        {
            get { return true; }
        }

        public Kinect()
        {
            OutPortData.Add(new PortData("right hand", "The 2D point of the right hand."));
            OutPortData.Add(new PortData("left Hand", "The 2D point of the left hand."));
            ArgumentLacing = LacingStrategy.Disabled;

            RegisterAllPorts();

            SetupKinect();
        }

        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
        {
            // Get skeleton data.
            var allSkeletons = runtime.SkeletonEngine.GetNextFrame(0);

            AssociativeNode rightHandNode = null;
            AssociativeNode leftHandNode = null;

            if (allSkeletons != null)
            {
                // Get the first tracked skeleton
                var skeleton = (from s in allSkeletons.Skeletons
                                where s.TrackingState == SkeletonTrackingState.Tracked
                                select s).FirstOrDefault();

                if (skeleton != null)
                {
                    var rightHand = skeleton.Joints[JointID.HandRight].ScaleTo(1,1);
                    var leftHand = skeleton.Joints[JointID.HandLeft].ScaleTo(1,1);

                    var rx = AstFactory.BuildDoubleNode(rightHand.Position.X);
                    var ry = AstFactory.BuildDoubleNode(rightHand.Position.Y);
                    var rz = AstFactory.BuildDoubleNode(rightHand.Position.Z);

                    rightHandNode = AstFactory.BuildFunctionCall(
                        new Func<double, double, double, Point>(Point.ByCoordinates),
                        new List<AssociativeNode> { rx, ry, rz });

                    var lx = AstFactory.BuildDoubleNode(leftHand.Position.X);
                    var ly = AstFactory.BuildDoubleNode(leftHand.Position.Y);
                    var lz = AstFactory.BuildDoubleNode(leftHand.Position.Z);

                    leftHandNode = AstFactory.BuildFunctionCall(
                        new Func<double, double, double, Point>(Point.ByCoordinates),
                        new List<AssociativeNode> { lx, ly, lz });
                }
            }
            else
            {
                rightHandNode = AstFactory.BuildNullNode();
                leftHandNode = AstFactory.BuildNullNode();
            }

            return new[]
            {
                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(0),
                    rightHandNode),
                AstFactory.BuildAssignment(
                    GetAstIdentifierForOutputIndex(1),
                    leftHandNode)
            };
        }

        public void SetupCustomUIElements(dynNodeView view)
        {
            int width = 320;
            int height = 240;

            var wbitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            // Create an array of pixels to contain pixel color values
            var pixels = new uint[width * height];

            int red;
            int green;
            int blue;
            int alpha;

            for (int x = 0; x < width; ++x)
            {
                for (int y = 0; y < height; ++y)
                {
                    int i = width * y + x;

                    red = 0;
                    green = 255 * y / height;
                    blue = 255 * (width - x) / width;
                    alpha = 255;

                    pixels[i] = (uint)((blue << 24) + (green << 16) + (red << 8) + alpha);
                }
            }

            // apply pixels to bitmap
            wbitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);

            image1 = new Image();
            image1.Width = width;
            image1.Height = height;
            image1.Margin = new Thickness(5);
            image1.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            image1.Name = "image1";
            image1.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            image1.Source = wbitmap;

            view.PresentationGrid.Children.Add(image1);
            view.PresentationGrid.Visibility = Visibility.Visible;

            runtime.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(RuntimeDepthFrameReady);
            runtime.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.Depth);
        }

        #region event handlers

        void RuntimeDepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            var planarImage = e.ImageFrame.Image;
            image1.Source = e.ImageFrame.ToBitmapSource();
        }

        #endregion

        #region private methods

        private void SetupKinect()
        {
            if (Runtime.Kinects.Count == 0)
            {
                Error("No kinect available");
            }
            else
            {
                // Use first Kinect
                // Initialize to return both Dpeth & Skeleton
                runtime = Runtime.Kinects[0];         
                runtime.Initialize(RuntimeOptions.UseDepth | RuntimeOptions.UseSkeletalTracking);
            }
        }

        #endregion

    }
}