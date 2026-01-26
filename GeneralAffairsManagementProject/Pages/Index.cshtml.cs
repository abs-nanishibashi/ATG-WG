using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;
using GeneralAffairsManagementProject.Models;

namespace GeneralAffairsManagementProject.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IDbConnection _db;

        public IndexModel(ILogger<IndexModel> logger, IDbConnection db)
        {
            _logger = logger;
            _db = db;
        }

        /// <summary>
        /// 検索条件
        /// </summary>
        [BindProperty]
        public OrderSearchCondition SearchCondition { get; set; } = new();

        /// <summary>
        /// 検索結果
        /// </summary>
        public List<OrderSearchResult> SearchResults { get; set; } = new();

        /// <summary>
        /// ページング情報
        /// </summary>
        public PagingInfo PagingInfo { get; set; } = new();

        /// <summary>
        /// 発注方法リスト
        /// </summary>
        public List<OrderingMethod> OrderingMethods { get; set; } = new();

        /// <summary>
        /// ステータスリスト
        /// </summary>
        public List<OrderStatus> OrderStatuses { get; set; } = new();

        /// <summary>
        /// バリデーションエラーメッセージ
        /// </summary>
        public Dictionary<string, string> ValidationErrors { get; set; } = new();

        /// <summary>
        /// 初期表示
        /// </summary>
        public async Task OnGetAsync()
        {
            // マスタデータ取得
            await LoadMasterDataAsync();

            // 初期条件で検索
            SearchCondition.IncludeExpiredNotDelivered = false;
            await ExecuteSearchAsync();
        }

        /// <summary>
        /// 検索ボタン押下
        /// </summary>
        public async Task<IActionResult> OnPostSearchAsync()
        {
            // マスタデータ取得
            await LoadMasterDataAsync();

            // 入力チェック
            if (!ValidateSearchCondition())
            {
                return Page();
            }

            // 1ページ目を表示
            SearchCondition.CurrentPage = 1;
            await ExecuteSearchAsync();

            return Page();
        }

        /// <summary>
        /// クリアボタン押下
        /// </summary>
        public async Task<IActionResult> OnPostClearAsync()
        {
            // マスタデータ取得
            await LoadMasterDataAsync();

            // 検索条件をクリア
            SearchCondition = new OrderSearchCondition
            {
                IncludeExpiredNotDelivered = false
            };

            // 検索結果は更新しない（前回の結果を保持）
            return Page();
        }

        /// <summary>
        /// ページング押下
        /// </summary>
        public async Task<IActionResult> OnPostPageAsync(int page)
        {
            // マスタデータ取得
            await LoadMasterDataAsync();

            // 指定ページで検索
            SearchCondition.CurrentPage = page;
            await ExecuteSearchAsync();

            return Page();
        }

        /// <summary>
        /// マスタデータ取得
        /// </summary>
        private async Task LoadMasterDataAsync()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // 発注方法マスタ取得
                const string methodSql = @"
                    SELECT ID, NAME
                    FROM TM_ORDERING_METHOD
                    WHERE DELETE_FLAG = 0
                    ORDER BY ID";

                using (var cmd = new SqlCommand(methodSql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    OrderingMethods = new List<OrderingMethod>();
                    while (await reader.ReadAsync())
                    {
                        OrderingMethods.Add(new OrderingMethod
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }

                // 発注ステータスマスタ取得
                const string statusSql = @"
                    SELECT ID, ORDER_STATUS_NAME
                    FROM TM_ORDER_STATUS
                    WHERE DELETE_FLAG = 0
                    ORDER BY ID";

                using (var cmd = new SqlCommand(statusSql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    OrderStatuses = new List<OrderStatus>();
                    while (await reader.ReadAsync())
                    {
                        OrderStatuses.Add(new OrderStatus
                        {
                            Id = reader.GetInt32(0),
                            OrderStatusName = reader.GetString(1)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 入力チェック
        /// </summary>
        private bool ValidateSearchCondition()
        {
            ValidationErrors.Clear();
            bool isValid = true;

            // 発注日チェック
            if (SearchCondition.OrderDateFrom.HasValue && SearchCondition.OrderDateTo.HasValue)
            {
                if (SearchCondition.OrderDateFrom.Value > SearchCondition.OrderDateTo.Value)
                {
                    ValidationErrors["OrderDate"] = "発注日（From）は発注日（To）以前の日付を指定してください。";
                    isValid = false;
                }
            }

            // 納品日チェック
            if (SearchCondition.DeliveryDateFrom.HasValue && SearchCondition.DeliveryDateTo.HasValue)
            {
                if (SearchCondition.DeliveryDateFrom.Value > SearchCondition.DeliveryDateTo.Value)
                {
                    ValidationErrors["DeliveryDate"] = "納品日（From）は納品日（To）以前の日付を指定してください。";
                    isValid = false;
                }
            }

            return isValid;
        }

        /// <summary>
        /// 検索実行
        /// </summary>
        private async Task ExecuteSearchAsync()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // 検索条件構築
                var whereClauses = new List<string> { "o.DELETE_FLAG = 0" };
                var parameters = new List<SqlParameter>();

                // 発注方法（消耗品マスタ経由で判定）
                if (SearchCondition.OrderingMethodId.HasValue)
                {
                    whereClauses.Add(@"EXISTS (
                        SELECT 1 
                        FROM TD_ORDER_DETAILS od
                        INNER JOIN TM_CONSUMABLES c ON od.CONSUMABLES_ID = c.ID
                        INNER JOIN TM_CONSUMABLES_CATEGORY cc ON c.CATEGORY_ID = cc.ID
                        WHERE od.ORDER_ID = o.ID 
                        AND cc.METHOD_ID = @MethodId
                        AND od.DELETE_FLAG = 0
                    )");
                    parameters.Add(new SqlParameter("@MethodId", SearchCondition.OrderingMethodId.Value));
                }

                // 発注者（前方一致）
                if (!string.IsNullOrWhiteSpace(SearchCondition.OrderUser))
                {
                    whereClauses.Add("o.ORDER_USER_NAME LIKE @OrderUser");
                    parameters.Add(new SqlParameter("@OrderUser", SearchCondition.OrderUser + "%"));
                }

                // 品目番号（前方一致）
                if (!string.IsNullOrWhiteSpace(SearchCondition.ItemNumber))
                {
                    whereClauses.Add(@"EXISTS (
                        SELECT 1 
                        FROM TD_ORDER_DETAILS od
                        INNER JOIN TM_CONSUMABLES c ON od.CONSUMABLES_ID = c.ID
                        WHERE od.ORDER_ID = o.ID 
                        AND c.ITEM_NO LIKE @ItemNumber
                        AND od.DELETE_FLAG = 0
                    )");
                    parameters.Add(new SqlParameter("@ItemNumber", SearchCondition.ItemNumber + "%"));
                }

                // ステータス
                if (SearchCondition.StatusId.HasValue)
                {
                    whereClauses.Add("o.ORDER_STATUS_ID = @StatusId");
                    parameters.Add(new SqlParameter("@StatusId", SearchCondition.StatusId.Value));
                }

                // 発注日From
                if (SearchCondition.OrderDateFrom.HasValue)
                {
                    whereClauses.Add("o.ORDER_DATE >= @OrderDateFrom");
                    parameters.Add(new SqlParameter("@OrderDateFrom", SearchCondition.OrderDateFrom.Value.Date));
                }

                // 発注日To
                if (SearchCondition.OrderDateTo.HasValue)
                {
                    whereClauses.Add("o.ORDER_DATE < @OrderDateTo");
                    parameters.Add(new SqlParameter("@OrderDateTo", SearchCondition.OrderDateTo.Value.Date.AddDays(1)));
                }

                // 納品日From
                if (SearchCondition.DeliveryDateFrom.HasValue)
                {
                    whereClauses.Add(@"EXISTS (
                        SELECT 1 
                        FROM TD_ORDER_DETAILS od
                        WHERE od.ORDER_ID = o.ID 
                        AND od.SCHEDULED_DELIVERY_DATE >= @DeliveryDateFrom
                        AND od.DELETE_FLAG = 0
                    )");
                    parameters.Add(new SqlParameter("@DeliveryDateFrom", SearchCondition.DeliveryDateFrom.Value.Date));
                }

                // 納品日To
                if (SearchCondition.DeliveryDateTo.HasValue)
                {
                    whereClauses.Add(@"EXISTS (
                        SELECT 1 
                        FROM TD_ORDER_DETAILS od
                        WHERE od.ORDER_ID = o.ID 
                        AND od.SCHEDULED_DELIVERY_DATE < @DeliveryDateTo
                        AND od.DELETE_FLAG = 0
                    )");
                    parameters.Add(new SqlParameter("@DeliveryDateTo", SearchCondition.DeliveryDateTo.Value.Date.AddDays(1)));
                }

                // 有効期限切れ且つ未納品を含まない場合の除外条件
                if (!SearchCondition.IncludeExpiredNotDelivered)
                {
                    whereClauses.Add(@"NOT EXISTS (
                        SELECT 1 
                        FROM TD_ORDER_DETAILS od
                        INNER JOIN TM_CONSUMABLES c ON od.CONSUMABLES_ID = c.ID
                        WHERE od.ORDER_ID = o.ID 
                        AND c.EXPIRATION_DATE < GETDATE()
                        AND od.DELIVERYED_FLAG = 0
                        AND od.DELETE_FLAG = 0
                    )");
                }

                var whereClause = string.Join(" AND ", whereClauses);

                // 総件数取得
                var countSql = $@"
                    SELECT COUNT(*)
                    FROM TD_ORDER o
                    WHERE {whereClause}";

                using (var cmd = new SqlCommand(countSql, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());
                    PagingInfo.TotalCount = (int)await cmd.ExecuteScalarAsync();
                    PagingInfo.CurrentPage = SearchCondition.CurrentPage;
                }

                // データ取得（ページング）
                var offset = (SearchCondition.CurrentPage - 1) * PagingInfo.PageSize;
                var dataSql = $@"
                    SELECT 
                        o.ID,
                        o.ORDER_NO,
                        o.ORDER_DATE,
                        o.ORDER_USER_NAME,
                        os.ORDER_STATUS_NAME,
                        (
                            SELECT STRING_AGG(c.ITEM_NO, ',') WITHIN GROUP (ORDER BY c.ITEM_NO)
                            FROM TD_ORDER_DETAILS od
                            INNER JOIN TM_CONSUMABLES c ON od.CONSUMABLES_ID = c.ID
                            WHERE od.ORDER_ID = o.ID AND od.DELETE_FLAG = 0
                        ) AS ITEM_NUMBERS,
                        (
                            SELECT SUM(od.ORDERING_TIME_UNIT_PRICE * od.ORDER_QUANTITY)
                            FROM TD_ORDER_DETAILS od
                            WHERE od.ORDER_ID = o.ID AND od.DELETE_FLAG = 0
                        ) AS TOTAL_PRICE,
                        (
                            SELECT TOP 1 om.NAME
                            FROM TD_ORDER_DETAILS od
                            INNER JOIN TM_CONSUMABLES c ON od.CONSUMABLES_ID = c.ID
                            INNER JOIN TM_CONSUMABLES_CATEGORY cc ON c.CATEGORY_ID = cc.ID
                            INNER JOIN TM_ORDERING_METHOD om ON cc.METHOD_ID = om.ID
                            WHERE od.ORDER_ID = o.ID AND od.DELETE_FLAG = 0
                            ORDER BY om.ID
                        ) AS ORDERING_METHOD_NAME
                    FROM TD_ORDER o
                    INNER JOIN TM_ORDER_STATUS os ON o.ORDER_STATUS_ID = os.ID
                    WHERE {whereClause}
                    ORDER BY o.ORDER_DATE DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY";

                using (var cmd = new SqlCommand(dataSql, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());
                    cmd.Parameters.Add(new SqlParameter("@Offset", offset));
                    cmd.Parameters.Add(new SqlParameter("@PageSize", PagingInfo.PageSize));

                    using var reader = await cmd.ExecuteReaderAsync();
                    SearchResults = new List<OrderSearchResult>();

                    while (await reader.ReadAsync())
                    {
                        SearchResults.Add(new OrderSearchResult
                        {
                            OrderId = reader.GetInt32(0),
                            OrderIdDisplay = reader.IsDBNull(1) ? $"ORD-{reader.GetInt32(0):D6}" : reader.GetString(1),
                            OrderDate = reader.GetDateTime(2),
                            OrderUser = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            OrderStatusName = reader.GetString(4),
                            ItemNumbers = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            TotalPrice = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                            OrderingMethodName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
                        });
                    }
                }
            }
        }
    }
}
