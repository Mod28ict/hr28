namespace HR28.Web.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        /// <summary>Plain-language explanation; the generic text is shown when empty.</summary>
        public string? Message { get; set; }
    }
}
