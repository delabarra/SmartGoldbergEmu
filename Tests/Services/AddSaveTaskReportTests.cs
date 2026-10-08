using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class AddSaveTaskReportTests
    {
        [Fact]
        public void Step_message_passes_through_when_no_images_are_downloading()
        {
            var strip = new RecordingTaskReport();
            var report = new AddSaveTaskReport(strip);

            report.SetMessage("Saving settings…");

            Assert.Equal("Saving settings…", strip.Text);
        }

        [Fact]
        public void Image_progress_leaves_step_text_alone_and_drives_the_bar_when_no_step_progress()
        {
            var strip = new RecordingTaskReport();
            var report = new AddSaveTaskReport(strip);

            report.SetMessage("Generating metadata…");
            report.ReportImageProgress(3, 10);

            Assert.Equal("Generating metadata…", strip.Text);
            Assert.Equal(3, strip.ProgressCurrent);
            Assert.Equal(10, strip.ProgressTotal);
        }

        [Fact]
        public void Image_counter_is_shown_as_text_only_without_a_step_message()
        {
            var strip = new RecordingTaskReport();
            var report = new AddSaveTaskReport(strip);

            report.ReportImageProgress(3, 10);

            Assert.Equal("Downloading library images 3/10", strip.Text);

            report.EndImageProgress();

            Assert.Equal(string.Empty, strip.Text);
        }

        [Fact]
        public void Step_progress_owns_the_bar_while_images_keep_counting()
        {
            var strip = new RecordingTaskReport();
            var report = new AddSaveTaskReport(strip);

            report.SetMessage("Downloading achievement icons 1/40");
            report.SetProgress(1, 40);
            report.ReportImageProgress(5, 10);

            Assert.Equal("Downloading achievement icons 1/40", strip.Text);
            Assert.Equal(1, strip.ProgressCurrent);
            Assert.Equal(40, strip.ProgressTotal);

            report.SetProgress(0, 0);

            Assert.Equal(5, strip.ProgressCurrent);
            Assert.Equal(10, strip.ProgressTotal);
        }

        [Fact]
        public void Finished_images_drop_the_counter_and_hide_the_image_bar()
        {
            var strip = new RecordingTaskReport();
            var report = new AddSaveTaskReport(strip);

            report.SetMessage("Saving settings…");
            report.ReportImageProgress(9, 10);
            report.ReportImageProgress(10, 10);

            Assert.Equal("Saving settings…", strip.Text);
            Assert.Equal(0, strip.ProgressTotal);
        }

        [Fact]
        public void End_image_progress_clears_a_counter_that_never_reached_its_total()
        {
            var strip = new RecordingTaskReport();
            var report = new AddSaveTaskReport(strip);

            report.SetMessage("Waiting for images…");
            report.ReportImageProgress(4, 10);
            report.EndImageProgress();

            Assert.Equal("Waiting for images…", strip.Text);
            Assert.Equal(0, strip.ProgressTotal);
        }

        [Fact]
        public void Auto_clear_is_deferred_while_images_are_downloading()
        {
            var strip = new RecordingTaskReport();
            var report = new AddSaveTaskReport(strip);

            report.ReportImageProgress(2, 10);
            report.SetMessageWithAutoClear("App info exported.");

            Assert.Equal(0, strip.AutoClearCount);
            Assert.Equal("App info exported.", strip.Text);

            report.EndImageProgress();
            report.SetMessageWithAutoClear("Game added to the library.");

            Assert.Equal(1, strip.AutoClearCount);
            Assert.Equal("Game added to the library.", strip.Text);
        }

        private sealed class RecordingTaskReport : ITaskReportService
        {
            public string Text { get; private set; } = string.Empty;
            public int ProgressCurrent { get; private set; }
            public int ProgressTotal { get; private set; }
            public int AutoClearCount { get; private set; }

            public void SetMessage(string message)
            {
                SetMessage(message, TaskReportKind.Info);
            }

            public void SetMessage(string message, TaskReportKind kind)
            {
                Text = message ?? string.Empty;
            }

            public void SetProgress(int current, int total)
            {
                ProgressCurrent = total > 0 ? current : 0;
                ProgressTotal = total > 0 ? total : 0;
            }

            public void SetMessageWithAutoClear(string message, TaskReportKind kind = TaskReportKind.Info, int delayMs = TaskReportDefaults.AutoClearDelayMs)
            {
                AutoClearCount++;
                Text = message ?? string.Empty;
                ProgressCurrent = 0;
                ProgressTotal = 0;
            }
        }
    }
}
