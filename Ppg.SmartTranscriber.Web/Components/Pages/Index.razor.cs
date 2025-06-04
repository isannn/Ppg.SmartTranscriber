using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ppg.SmartTranscriber.Web.Components.Pages
{
    public class IndexBase : ComponentBase
    {
        [Inject]
        public HttpClient Http { get; set; } = default!;

        protected IBrowserFile? SelectedFile { get; set; }
        protected bool IsFileSelected => SelectedFile != null;
        protected UploadResultModel? UploadResult { get; set; }

        protected void OnInputFileChange(InputFileChangeEventArgs e)
        {
            SelectedFile = e.File;
            UploadResult = null;
        }

        protected async Task UploadFileAsync()
        {
            if (SelectedFile == null)
                return;

            var content = new MultipartFormDataContent();
            var stream = SelectedFile.OpenReadStream(50 * 1024 * 1024); // 50 MB limit
            content.Add(new StreamContent(stream)
            {
                Headers =
                {
                    ContentLength = SelectedFile.Size,
                    ContentType = new MediaTypeHeaderValue(SelectedFile.ContentType)
                }
            }, "file", SelectedFile.Name);

            var response = await Http.PostAsync("/api/file/UploadAsset", content);

            if (response.IsSuccessStatusCode)
            {
                UploadResult = await response.Content.ReadFromJsonAsync<UploadResultModel>();
            }
            else
            {
                UploadResult = new UploadResultModel
                {
                    JobId = "N/A",
                    Status = "Upload failed"
                };
            }
        }

        public class UploadResultModel
        {
            public string JobId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}