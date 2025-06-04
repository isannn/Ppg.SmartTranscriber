using Microsoft.AspNetCore.Components;

using System.Net.Http.Json;

namespace Ppg.SmartTranscriber.Web.Components.Pages
{
    public class TranscriptionsBase : ComponentBase
    {
        [Inject]
        public HttpClient Http { get; set; } = default!;

        public List<TranscriptionJobViewModel> Jobs { get; set; } = new();
        public bool IsLoading { get; set; } = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadJobsAsync();
        }

        protected async Task LoadJobsAsync()
        {
            IsLoading = true;
            Jobs = await Http.GetFromJsonAsync<List<TranscriptionJobViewModel>>("/api/jobs") ?? new List<TranscriptionJobViewModel>();
            IsLoading = false;
            StateHasChanged();
        }

        protected async Task RefreshJobAsync(Guid jobId)
        {
            // Optionally, refresh a single job or reload all
            await LoadJobsAsync();
        }

        public class TranscriptionJobViewModel
        {
            public Guid Id { get; set; }
            public string ObjectName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public string? TranscriptText { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}