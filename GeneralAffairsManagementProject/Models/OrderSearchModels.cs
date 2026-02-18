using System.ComponentModel.DataAnnotations;

namespace GeneralAffairsManagementProject.Models
{
    /// <summary>
    /// 発注検索条件
    /// </summary>
    public class OrderSearchCondition
    {
        /// <summary>
        /// 発注方法ID
        /// </summary>
        public int? OrderingMethodId { get; set; }

        /// <summary>
        /// 発注者（前方一致検索）
        /// </summary>
        public string? OrderUser { get; set; }

        /// <summary>
        /// 有効期限切れ且つ未納品を含む
        /// </summary>
        public bool IncludeExpiredNotDelivered { get; set; }

        /// <summary>
        /// 品目番号（前方一致検索）
        /// </summary>
        public string? ItemNumber { get; set; }

        /// <summary>
        /// ステータスID
        /// </summary>
        public int? StatusId { get; set; }

        /// <summary>
        /// 発注日From
        /// </summary>
        [Display(Name = "発注日（From）")]
        public DateTime? OrderDateFrom { get; set; }

        /// <summary>
        /// 発注日To
        /// </summary>
        [Display(Name = "発注日（To）")]
        public DateTime? OrderDateTo { get; set; }

        /// <summary>
        /// 納品日From
        /// </summary>
        [Display(Name = "納品日（From）")]
        public DateTime? DeliveryDateFrom { get; set; }

        /// <summary>
        /// 納品日To
        /// </summary>
        [Display(Name = "納品日（To）")]
        public DateTime? DeliveryDateTo { get; set; }

        /// <summary>
        /// 現在のページ番号
        /// </summary>
        public int CurrentPage { get; set; } = 1;
    }

    /// <summary>
    /// 発注検索結果
    /// </summary>
    public class OrderSearchResult
    {
        /// <summary>
        /// 発注ID（システムID）
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// 発注ID（表示用）
        /// </summary>
        public string OrderIdDisplay { get; set; } = string.Empty;

        /// <summary>
        /// 発注日
        /// </summary>
        public DateTime OrderDate { get; set; }

        /// <summary>
        /// 発注者
        /// </summary>
        public string OrderUser { get; set; } = string.Empty;

        /// <summary>
        /// 発注方法
        /// </summary>
        public string OrderingMethodName { get; set; } = string.Empty;

        /// <summary>
        /// 発注品番（カンマ区切り）
        /// </summary>
        public string ItemNumbers { get; set; } = string.Empty;

        /// <summary>
        /// 合計金額
        /// </summary>
        public int TotalPrice { get; set; }

        /// <summary>
        /// 発注ステータス
        /// </summary>
        public string OrderStatusName { get; set; } = string.Empty;
    }

    /// <summary>
    /// ページング情報
    /// </summary>
    public class PagingInfo
    {
        /// <summary>
        /// 総件数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 1ページあたりの表示件数
        /// </summary>
        public int PageSize { get; set; } = 5;

        /// <summary>
        /// 現在のページ番号
        /// </summary>
        public int CurrentPage { get; set; } = 1;

        /// <summary>
        /// 総ページ数
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>
        /// 前のページが存在するか
        /// </summary>
        public bool HasPreviousPage => CurrentPage > 1;

        /// <summary>
        /// 次のページが存在するか
        /// </summary>
        public bool HasNextPage => CurrentPage < TotalPages;

        /// <summary>
        /// 表示するページ番号リスト（最大5ページ）
        /// </summary>
        public List<int> PageNumbers
        {
            get
            {
                var pages = new List<int>();
                var startPage = Math.Max(1, CurrentPage - 2);
                var endPage = Math.Min(TotalPages, startPage + 4);

                // 5ページ表示を維持するため開始ページを調整
                if (endPage - startPage < 4)
                {
                    startPage = Math.Max(1, endPage - 4);
                }

                for (int i = startPage; i <= endPage; i++)
                {
                    pages.Add(i);
                }

                return pages;
            }
        }
    }

    /// <summary>
    /// 発注方法マスタ
    /// </summary>
    public class OrderingMethod
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 発注ステータスマスタ
    /// </summary>
    public class OrderStatus
    {
        public int Id { get; set; }
        public string OrderStatusName { get; set; } = string.Empty;
    }
}