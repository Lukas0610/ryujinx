using System;

namespace Ryujinx.UI.Helpers
{

    public sealed class UIProgressReporter
    {

        public event EventHandler<UIProgressEventArgs> ProgressChanged;
        public event EventHandler Finished;
        public event EventHandler Cancelled;

        public ProgressType Type { get; set; }

        public UIProgressReporter() { }

        /// <summary>
        /// Indicate a name-only progress
        /// </summary>
        /// <param name="text">Text to be shown with the progress (e.g. the file currently being processed)</param>
        public void ReportProgress(string text)
        {
            ProgressChanged?.Invoke(this, new UIProgressEventArgs(text));
        }

        /// <summary>
        /// Indicate a progress of indeterminate length
        /// </summary>
        /// <param name="text">Text to be shown with the progress (e.g. the file currently being processed)</param>
        /// <param name="current">The current progress value (e.g. the current position in the file)</param>
        public void ReportProgress(string text, long current)
        {
            ProgressChanged?.Invoke(this, new UIProgressEventArgs(text, current));
        }

        /// <summary>
        /// Indicate a progress of indeterminate length and with calculated speed
        /// </summary>
        /// <param name="text">Text to be shown with the progress (e.g. the file currently being processed)</param>
        /// <param name="current">The current progress value (e.g. the current position in the file)</param>
        /// <param name="speed">The speed in bits per second</param>
        public void ReportProgress(string text, long current, double speed)
        {
            ProgressChanged?.Invoke(this, new UIProgressEventArgs(text, current, speed));
        }

        /// <summary>
        /// Indicate a progress of known length and with calculated speed
        /// </summary>
        /// <param name="text">Text to be shown with the progress (e.g. the file currently being processed)</param>
        /// <param name="current">The current progress value (e.g. the current position in the file)</param>
        /// <param name="total">The total progress length (e.g. the size of the file)</param>
        /// <param name="speed">The speed in bits per second</param>
        public void ReportProgress(string text, long current, long total, double speed)
        {
            ProgressChanged?.Invoke(this, new UIProgressEventArgs(text, current, total, speed));
        }

        /// <summary>
        /// Notify a consumer (e.g. a progress dialog) that the event has been finished (e.g. to close the progress dialog)
        /// </summary>
        public void Finish()
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Notify a producer (e.g. the logic giving the progress) that the event has been cancelled
        /// </summary>
        public void Cancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }

    }

}
