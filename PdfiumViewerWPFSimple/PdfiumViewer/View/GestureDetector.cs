/*
 * Created by SharpDevelop.
 * User: 
 * Date: 2017/6/21
 * Time: 14:37
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Windows;

namespace PdfiumViewer
{
	/// <summary>
	/// Description of GestureDetector.
	/// </summary>
	public class GestureDetector
	{
       private readonly uint _pixelPerCm = 38;
        private bool _isGestureDetected = false;

        public bool IsPanningAllowed { get; private set; }
        public bool IsScalingAllowed { get; private set; }
        public bool IsRotatingAllowed { get; private set; }

        double scale = 0.0d;
        double rot = 0.0d;

        private FrameworkElement _uiElement;

        public GestureDetector(FrameworkElement uiElement)
        {
            _uiElement = uiElement;
            IsPanningAllowed = false;
            IsScalingAllowed = false;
            IsRotatingAllowed = false;

            uiElement.ManipulationStarted += uiElement_ManipulationStarted;
            uiElement.ManipulationDelta += uiElement_ManipulationDelta;
            uiElement.ManipulationCompleted += uiElement_ManipulationCompleted;
        }

        public void unload()
        {
            _uiElement.ManipulationStarted -= uiElement_ManipulationStarted;
            _uiElement.ManipulationDelta -= uiElement_ManipulationDelta;
            _uiElement.ManipulationCompleted -= uiElement_ManipulationCompleted;
        }

        void uiElement_ManipulationStarted(object sender, System.Windows.Input.ManipulationStartedEventArgs e)
        {
            IsPanningAllowed = true;
        }

        void uiElement_ManipulationDelta(object sender, System.Windows.Input.ManipulationDeltaEventArgs e)
        {
            const double MIN_SCALE_TRIGGER = 0.05;
            const int MIN_ROTATIONANGLE_TRIGGER_DEGREE = 10;
            const int MIN_FINGER_DISTANCE_FOR_ROTATION_CM = 2;

            var manipulatorBounds = Rect.Empty;
            foreach (var manipulator in e.Manipulators)
            {
                manipulatorBounds.Union(manipulator.GetPosition(sender as IInputElement));
            }

            var distance = (manipulatorBounds.TopLeft - manipulatorBounds.BottomRight).Length;
            var distanceInCm = distance / _pixelPerCm;

            scale += 1 - (e.DeltaManipulation.Scale.Length / Math.Sqrt(2));

            rot += e.DeltaManipulation.Rotation;

            if (Math.Abs(scale) > MIN_SCALE_TRIGGER && Math.Abs(rot) < MIN_ROTATIONANGLE_TRIGGER_DEGREE)
            {
                ApplyScaleMode();
            }

            if (Math.Abs(rot) >= MIN_ROTATIONANGLE_TRIGGER_DEGREE && distanceInCm > MIN_FINGER_DISTANCE_FOR_ROTATION_CM)
            {
                ApplyRotationMode();
            }
        }

        void uiElement_ManipulationCompleted(object sender, System.Windows.Input.ManipulationCompletedEventArgs e)
        {
            scale = 0.0d;
            rot = 0.0d;
            IsPanningAllowed = false;
            IsScalingAllowed = false;
            IsRotatingAllowed = false;
            _isGestureDetected = false;
        }

        private void ApplyScaleMode()
        {
            if (!_isGestureDetected)
            {
                _isGestureDetected = true;
                IsPanningAllowed = true;
                IsScalingAllowed = true;
                IsRotatingAllowed = false;
            }
        }

        private void ApplyRotationMode()
        {
            if (!_isGestureDetected)
            {
                _isGestureDetected = true;
                IsPanningAllowed = true;
                IsScalingAllowed = true;
                IsRotatingAllowed = true;
            }
        }
	}
}
