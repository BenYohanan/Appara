using X.PagedList;

namespace Core.ViewModels
{
	public class IPageListModel<T> : IIPageListModel
	{
		public IPageListModel()
		{
			CanFilterByDateRange = true;
		}
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		private string? _keyword;
		public string? Keyword
		{
			get => string.IsNullOrWhiteSpace(_keyword) ? null : _keyword.Trim().ToLower();
			set => _keyword = value;
		}
		public string? SearchAction { get; set; }
		public string? SearchController { get; set; }
		public IPagedList<T> Model { get; set; }
		public bool CanFilterByDateRange { get; set; }
		public DeliveryStatus DeliveryStatus { get; set; }
		public bool CanFilterByDeliveryStatus { get; set; }

		public string? UserId { get; set; }
	}
	public interface IIPageListModel
	{
		DateTime? EndDate { get; set; }
		string? Keyword { get; set; }
		string? SearchAction { get; set; }
		string? SearchController { get; set; }
		DateTime? StartDate { get; set; }
		bool CanFilterByDateRange { get; set; }
		DeliveryStatus DeliveryStatus { get; set; }
		bool CanFilterByDeliveryStatus { get; set; }
	}
    public enum DeliveryStatus
    {
        Sent = 1,
        Failed = 2,
    }
}
