using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace Ppg.SmartTranscriber.Web.Components.Pages
{
    public class TranscriptViewBase : ComponentBase
    {
        [Parameter]
        public Guid JobId { get; set; }

        [Inject]
        public HttpClient Http { get; set; } = default!;

        public List<SegmentViewModel> Segments { get; set; } = new();

        protected Dictionary<string, string> SpeakerColors { get; set; } = new();
        private readonly string[] ColorClasses = { "table-primary", "table-success", "table-warning", "table-info", "table-light", "table-secondary" };

        protected override async Task OnInitializedAsync()
        {
            // Load segments for the job (replace with your actual API endpoint)
            Segments = await Http.GetFromJsonAsync<List<SegmentViewModel>>($"/api/jobs/{JobId}/segments") ?? new List<SegmentViewModel>();

            // Assign color classes to speakers
            var distinctSpeakers = Segments.Select(s => s.Speaker).Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            for (int i = 0; i < distinctSpeakers.Count; i++)
            {
                SpeakerColors[distinctSpeakers[i]] = ColorClasses[i % ColorClasses.Length];
            }
        }

        protected string GetSpeakerColorClass(string? speaker)
        {
            if (!string.IsNullOrWhiteSpace(speaker) && SpeakerColors.ContainsKey(speaker))
                return SpeakerColors[speaker];
            return "";
        }

        protected async Task SaveSpeakerAsync(SegmentViewModel segment)
        {
            var response = await Http.PutAsJsonAsync($"/api/segments/{segment.Id}/speaker", new { speaker = segment.Speaker });
            if (response.IsSuccessStatusCode)
            {
                segment.IsValidSpeaker = true;
                segment.IsInvalidSpeaker = false;
            }
            else
            {
                segment.IsValidSpeaker = false;
                segment.IsInvalidSpeaker = true;
            }
            StateHasChanged();
            await Task.Delay(1500);
            segment.IsValidSpeaker = false;
            segment.IsInvalidSpeaker = false;
            StateHasChanged();
        }

        protected void OnSpeakerChanged(SegmentViewModel segment)
        {
            // Optionally handle speaker change event
        }

        public class SegmentViewModel
        {
            public Guid Id { get; set; }
            public TimeSpan StartTime { get; set; }
            public TimeSpan EndTime { get; set; }
            public string Speaker { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;

            // For UI feedback
            public bool IsValidSpeaker { get; set; }
            public bool IsInvalidSpeaker { get; set; }
        }
    }
}
