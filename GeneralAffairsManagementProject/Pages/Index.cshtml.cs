�ｿusing Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;
using GeneralAffairsManagementProject.Models;
using System.Text.Json;

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
        /// 讀懃ｴ｢譚｡莉ｶ
        /// </summary>
        [BindProperty]
        public OrderSearchCondition SearchCondition { get; set; } = new();

        /// <summary>
        /// 讀懃ｴ｢邨先棡
        /// </summary>
        public List<OrderSearchResult> SearchResults { get; set; } = new();

        /// <summary>
        /// 繝壹�繧ｸ繝ｳ繧ｰ諠�ｱ
        /// </summary>
        public PagingInfo PagingInfo { get; set; } = new();

        /// <summary>
        /// 逋ｺ豕ｨ譁ｹ豕輔Μ繧ｹ繝
        /// </summary>
        public List<OrderingMethod> OrderingMethods { get; set; } = new();

        /// <summary>
        /// 繧ｹ繝��繧ｿ繧ｹ繝ｪ繧ｹ繝
        /// </summary>
        public List<OrderStatus> OrderStatuses { get; set; } = new();

        /// <summary>
        /// 繝舌Μ繝��繧ｷ繝ｧ繝ｳ繧ｨ繝ｩ繝ｼ繝｡繝�そ繝ｼ繧ｸ
        /// </summary>
        public Dictionary<string, string> ValidationErrors { get; set; } = new();

        /// <summary>
        /// 蛻晄悄陦ｨ遉ｺ
        /// </summary>
        public async Task OnGetAsync()
        {
            _logger.LogInformation("Order list initial display started.");
            
            try
            {
                // 繝槭せ繧ｿ繝��繧ｿ蜿門ｾ
                await LoadMasterDataAsync();

                // 蛻晄悄譚｡莉ｶ縺ｧ讀懃ｴ｢
                SearchCondition.IncludeExpiredNotDelivered = false;
                await ExecuteSearchAsync();
                
                // 蛻晏屓讀懃ｴ｢邨先棡繧剃ｿ晏ｭ
                SaveSearchConditionToTempData();
                SaveSearchResultsToTempData();
                
                _logger.LogInformation("Initial display completed. Result count: {ResultCount}", SearchResults.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during initial display.");
                throw;
            }
        }

        /// <summary>
        /// 讀懃ｴ｢繝懊ち繝ｳ謚ｼ荳
        /// </summary>
        public async Task<IActionResult> OnPostSearchAsync()
        {
            _logger.LogInformation("Search started. Condition: {@SearchCondition}", SearchCondition);
            
            try
            {
                // 繝槭せ繧ｿ繝��繧ｿ蜿門ｾ
                await LoadMasterDataAsync();

                // 蜈･蜉帙メ繧ｧ繝�け
                if (!ValidateSearchCondition())
                {
                    _logger.LogWarning("Validation error occurred. Errors: {@ValidationErrors}", ValidationErrors);
                    // 繝舌Μ繝��繧ｷ繝ｧ繝ｳ繧ｨ繝ｩ繝ｼ譎ゅ�莉･蜑阪�讀懃ｴ｢邨先棡繧貞ｾｩ蜈
                    RestoreSearchResultsFromTempData();
                    return Page();
                }

                // 1繝壹�繧ｸ逶ｮ繧定｡ｨ遉ｺ
                SearchCondition.CurrentPage = 1;
                await ExecuteSearchAsync();

                // 讀懃ｴ｢譚｡莉ｶ縺ｨ邨先棡繧探empData縺ｫ菫晏ｭ
                SaveSearchConditionToTempData();
                SaveSearchResultsToTempData();

                _logger.LogInformation("Search completed. Result count: {ResultCount}, Total count: {TotalCount}", 
                    SearchResults.Count, PagingInfo.TotalCount);

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during search. Condition: {@SearchCondition}", SearchCondition);
                throw;
            }
        }

        /// <summary>
        /// 繧ｯ繝ｪ繧｢繝懊ち繝ｳ謚ｼ荳
        /// </summary>
        public IActionResult OnPostClear()
        {
            _logger.LogInformation("Clear search condition started.");
            
            try
            {
                // 繝槭せ繧ｿ繝��繧ｿ蜿門ｾ暦ｼ亥酔譛溽沿�
                LoadMasterData();

                // TempData縺九ｉ讀懃ｴ｢邨先棡繧貞ｾｩ蜈
                RestoreSearchResultsFromTempData();

                // 讀懃ｴ｢譚｡莉ｶ繧偵け繝ｪ繧｢�医Δ繝�Ν繝舌う繝ｳ繝�ぅ繝ｳ繧ｰ蜑阪�迥ｶ諷九↓謌ｻ縺呻ｼ
                ModelState.Clear();
                SearchCondition = new OrderSearchCondition
                {
                    IncludeExpiredNotDelivered = false
                };

                // 讀懃ｴ｢邨先棡縺ｯTempData縺ｫ蜀堺ｿ晏ｭ假ｼ域ｬ｡蝗槭�縺溘ａ縺ｫ菫晄戟�
                SaveSearchResultsToTempData();
                
                // 讀懃ｴ｢譚｡莉ｶ縺ｯ繧ｯ繝ｪ繧｢縺励◆縺ｮ縺ｧTempData縺九ｉ蜑企勁
                TempData.Remove("SearchCondition");

                _logger.LogInformation("Clear search condition completed.");

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during clearing search condition.");
                throw;
            }
        }

        /// <summary>
        /// 繝壹�繧ｸ繝ｳ繧ｰ謚ｼ荳
        /// </summary>
        public async Task<IActionResult> OnPostPageAsync(int page)
        {
            _logger.LogInformation("Paging started. Page: {PageNumber}", page);
            
            try
            {
                // 繝槭せ繧ｿ繝��繧ｿ蜿門ｾ
                await LoadMasterDataAsync();

                // TempData縺九ｉ讀懃ｴ｢譚｡莉ｶ繧貞ｾｩ蜈
                RestoreSearchConditionFromTempData();

                // 謖�ｮ壹�繝ｼ繧ｸ縺ｧ讀懃ｴ｢
                SearchCondition.CurrentPage = page;
                await ExecuteSearchAsync();

                // 讀懃ｴ｢譚｡莉ｶ縺ｨ邨先棡繧貞�菫晏ｭ
                SaveSearchConditionToTempData();
                SaveSearchResultsToTempData();

                _logger.LogInformation("Paging completed. Page: {PageNumber}, Result count: {ResultCount}",
                    page, SearchResults.Count);

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during paging. Page: {PageNumber}", page);
                throw;
            }
        }

        /// <summary>
        /// 繝槭せ繧ｿ繝��繧ｿ蜿門ｾ暦ｼ亥酔譛溽沿�
        /// </summary>
        private void LoadMasterData()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    conn.Open();

                // 逋ｺ豕ｨ譁ｹ豕輔�繧ｹ繧ｿ蜿門ｾ
                const string methodSql = @"
                    SELECT ID, NAME
                    FROM TM_ORDERING_METHOD
                    WHERE DELETE_FLAG = 0
                    ORDER BY ID";

                using (var cmd = new SqlCommand(methodSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    OrderingMethods = new List<OrderingMethod>();
                    while (reader.Read())
                    {
                        OrderingMethods.Add(new OrderingMethod
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1)
                        });
                    }
                }

                // 逋ｺ豕ｨ繧ｹ繝��繧ｿ繧ｹ繝槭せ繧ｿ蜿門ｾ
                const string statusSql = @"
                    SELECT ID, ORDER_STATUS_NAME
                    FROM TM_ORDER_STATUS
                    WHERE DELETE_FLAG = 0
                    ORDER BY ID";

                using (var cmd = new SqlCommand(statusSql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    OrderStatuses = new List<OrderStatus>();
                    while (reader.Read())
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
        /// 繝槭せ繧ｿ繝��繧ｿ蜿門ｾ
        /// </summary>
        private async Task LoadMasterDataAsync()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // 逋ｺ豕ｨ譁ｹ豕輔�繧ｹ繧ｿ蜿門ｾ
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

                // 逋ｺ豕ｨ繧ｹ繝��繧ｿ繧ｹ繝槭せ繧ｿ蜿門ｾ
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
        /// 蜈･蜉帙メ繧ｧ繝�け
        /// </summary>
        private bool ValidateSearchCondition()
        {
            ValidationErrors.Clear();
            bool isValid = true;

            // 逋ｺ豕ｨ譌･繝√ぉ繝�け
            if (SearchCondition.OrderDateFrom.HasValue && SearchCondition.OrderDateTo.HasValue)
            {
                if (SearchCondition.OrderDateFrom.Value > SearchCondition.OrderDateTo.Value)
                {
                    ValidationErrors["OrderDate"] = "逋ｺ豕ｨ譌･(From)縺ｯ逋ｺ豕ｨ譌･(To)莉･蜑阪�譌･莉倥ｒ謖�ｮ壹＠縺ｦ縺上□縺輔＞縲";
                    isValid = false;
                }
            }

            // 邏榊刀譌･繝√ぉ繝�け
            if (SearchCondition.DeliveryDateFrom.HasValue && SearchCondition.DeliveryDateTo.HasValue)
            {
                if (SearchCondition.DeliveryDateFrom.Value > SearchCondition.DeliveryDateTo.Value)
                {
                    ValidationErrors["DeliveryDate"] = "邏榊刀譌･(From)縺ｯ邏榊刀譌･(To)莉･蜑阪�譌･莉倥ｒ謖�ｮ壹＠縺ｦ縺上□縺輔＞縲";
                    isValid = false;
                }
            }

            return isValid;
        }

        /// <summary>
        /// 讀懃ｴ｢螳溯｡
        /// </summary>
        private async Task ExecuteSearchAsync()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // 讀懃ｴ｢譚｡莉ｶ讒狗ｯ(WHERE蜿･縺ｨ繝代Λ繝｡繝ｼ繧ｿ繧堤函謌)
                var (whereClause, parameterFactory) = BuildSearchCondition();

                // 邱丈ｻｶ謨ｰ蜿門ｾ
                var countSql = $@"
                    SELECT COUNT(*)
                    FROM TD_ORDER o
                    WHERE {whereClause}";

                using (var cmd = new SqlCommand(countSql, conn))
                {
                    cmd.Parameters.AddRange(parameterFactory().ToArray());
                    var result = await cmd.ExecuteScalarAsync();
                    PagingInfo.TotalCount = result != null ? (int)result : 0;
                    PagingInfo.CurrentPage = SearchCondition.CurrentPage;
                }

                // 繝��繧ｿ蜿門ｾ(繝壹�繧ｸ繝ｳ繧ｰ)
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
                    cmd.Parameters.AddRange(parameterFactory().ToArray());
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

        /// <summary>
        /// 讀懃ｴ｢譚｡莉ｶ繧呈ｧ狗ｯ
        /// </summary>
        private (string whereClause, Func<List<SqlParameter>> parameterFactory) BuildSearchCondition()
        {
            var whereClauses = new List<string> { "o.DELETE_FLAG = 0" };

            // 繝代Λ繝｡繝ｼ繧ｿ繧堤函謌舌☆繧矩未謨ｰ繧定ｿ斐☆(蜻ｼ縺ｳ蜃ｺ縺吶◆縺ｳ縺ｫ譁ｰ縺励＞繧､繝ｳ繧ｹ繧ｿ繝ｳ繧ｹ繧堤函謌)
            Func<List<SqlParameter>> createParameters = () =>
            {
                var parameters = new List<SqlParameter>();

                // 逋ｺ豕ｨ譁ｹ豕(豸郁怜刀繝槭せ繧ｿ邨檎罰縺ｧ蛻､螳)
                if (SearchCondition.OrderingMethodId.HasValue)
                {
                    parameters.Add(new SqlParameter("@MethodId", SearchCondition.OrderingMethodId.Value));
                }

                // 逋ｺ豕ｨ閠(蜑肴婿荳閾ｴ)
                if (!string.IsNullOrWhiteSpace(SearchCondition.OrderUser))
                {
                    parameters.Add(new SqlParameter("@OrderUser", SearchCondition.OrderUser + "%"));
                }

                // 蜩∫岼逡ｪ蜿ｷ(蜑肴婿荳閾ｴ)
                if (!string.IsNullOrWhiteSpace(SearchCondition.ItemNumber))
                {
                    parameters.Add(new SqlParameter("@ItemNumber", SearchCondition.ItemNumber + "%"));
                }

                // 繧ｹ繝��繧ｿ繧ｹ
                if (SearchCondition.StatusId.HasValue)
                {
                    parameters.Add(new SqlParameter("@StatusId", SearchCondition.StatusId.Value));
                }

                // 逋ｺ豕ｨ譌･From
                if (SearchCondition.OrderDateFrom.HasValue)
                {
                    parameters.Add(new SqlParameter("@OrderDateFrom", SearchCondition.OrderDateFrom.Value.Date));
                }

                // 逋ｺ豕ｨ譌･To
                if (SearchCondition.OrderDateTo.HasValue)
                {
                    parameters.Add(new SqlParameter("@OrderDateTo", SearchCondition.OrderDateTo.Value.Date.AddDays(1)));
                }

                // 邏榊刀譌･From
                if (SearchCondition.DeliveryDateFrom.HasValue)
                {
                    parameters.Add(new SqlParameter("@DeliveryDateFrom", SearchCondition.DeliveryDateFrom.Value.Date));
                }

                // 邏榊刀譌･To
                if (SearchCondition.DeliveryDateTo.HasValue)
                {
                    parameters.Add(new SqlParameter("@DeliveryDateTo", SearchCondition.DeliveryDateTo.Value.Date.AddDays(1)));
                }

                return parameters;
            };

            // 逋ｺ豕ｨ譁ｹ豕(豸郁怜刀繝槭せ繧ｿ邨檎罰縺ｧ蛻､螳)
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
            }

            // 逋ｺ豕ｨ閠(蜑肴婿荳閾ｴ)
            if (!string.IsNullOrWhiteSpace(SearchCondition.OrderUser))
            {
                whereClauses.Add("o.ORDER_USER_NAME LIKE @OrderUser");
            }

            // 蜩∫岼逡ｪ蜿ｷ(蜑肴婿荳閾ｴ)
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
            }

            // 繧ｹ繝��繧ｿ繧ｹ
            if (SearchCondition.StatusId.HasValue)
            {
                whereClauses.Add("o.ORDER_STATUS_ID = @StatusId");
            }

            // 逋ｺ豕ｨ譌･From
            if (SearchCondition.OrderDateFrom.HasValue)
            {
                whereClauses.Add("o.ORDER_DATE >= @OrderDateFrom");
            }

            // 逋ｺ豕ｨ譌･To
            if (SearchCondition.OrderDateTo.HasValue)
            {
                whereClauses.Add("o.ORDER_DATE < @OrderDateTo");
            }

            // 邏榊刀譌･From
            if (SearchCondition.DeliveryDateFrom.HasValue)
            {
                whereClauses.Add(@"EXISTS (
                    SELECT 1 
                    FROM TD_ORDER_DETAILS od
                    WHERE od.ORDER_ID = o.ID 
                    AND od.SCHEDULED_DELIVERY_DATE >= @DeliveryDateFrom
                    AND od.DELETE_FLAG = 0
                )");
            }

            // 邏榊刀譌･To
            if (SearchCondition.DeliveryDateTo.HasValue)
            {
                whereClauses.Add(@"EXISTS (
                    SELECT 1 
                    FROM TD_ORDER_DETAILS od
                    WHERE od.ORDER_ID = o.ID 
                    AND od.SCHEDULED_DELIVERY_DATE < @DeliveryDateTo
                    AND od.DELETE_FLAG = 0
                )");
            }

            // 譛牙柑譛滄剞蛻�ｌ荳斐▽譛ｪ邏榊刀繧貞性縺ｾ縺ｪ縺�ｴ蜷医�髯､螟匁擅莉ｶ
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
            return (whereClause, createParameters);
        }

        /// <summary>
        /// 讀懃ｴ｢譚｡莉ｶ繧探empData縺ｫ菫晏ｭ
        /// </summary>
        private void SaveSearchConditionToTempData()
        {
            TempData["SearchCondition"] = JsonSerializer.Serialize(SearchCondition);
        }

        /// <summary>
        /// TempData縺九ｉ讀懃ｴ｢譚｡莉ｶ繧貞ｾｩ蜈
        /// </summary>
        private void RestoreSearchConditionFromTempData()
        {
            if (TempData.ContainsKey("SearchCondition"))
            {
                var searchConditionJson = TempData["SearchCondition"]?.ToString();
                if (!string.IsNullOrEmpty(searchConditionJson))
                {
                    SearchCondition = JsonSerializer.Deserialize<OrderSearchCondition>(searchConditionJson) ?? new();
                    // TempData繧貞�菫晏ｭ(谺｡蝗槭ｂ菴ｿ縺医ｋ繧医≧縺ｫ)
                    TempData["SearchCondition"] = searchConditionJson;
                }
            }
        }

        /// <summary>
        /// 讀懃ｴ｢邨先棡繧探empData縺ｫ菫晏ｭ
        /// </summary>
        private void SaveSearchResultsToTempData()
        {
            TempData["SearchResults"] = JsonSerializer.Serialize(SearchResults);
            TempData["PagingInfo"] = JsonSerializer.Serialize(PagingInfo);
        }

        /// <summary>
        /// TempData縺九ｉ讀懃ｴ｢邨先棡繧貞ｾｩ蜈
        /// </summary>
        private void RestoreSearchResultsFromTempData()
        {
            if (TempData.ContainsKey("SearchResults"))
            {
                var searchResultsJson = TempData["SearchResults"]?.ToString();
                if (!string.IsNullOrEmpty(searchResultsJson))
                {
                    SearchResults = JsonSerializer.Deserialize<List<OrderSearchResult>>(searchResultsJson) ?? new();
                    // TempData繧貞�菫晏ｭ(谺｡蝗槭ｂ菴ｿ縺医ｋ繧医≧縺ｫ)
                    TempData["SearchResults"] = searchResultsJson;
                }
            }

            if (TempData.ContainsKey("PagingInfo"))
            {
                var pagingInfoJson = TempData["PagingInfo"]?.ToString();
                if (!string.IsNullOrEmpty(pagingInfoJson))
                {
                    PagingInfo = JsonSerializer.Deserialize<PagingInfo>(pagingInfoJson) ?? new();
                    // TempData繧貞�菫晏ｭ(谺｡蝗槭ｂ菴ｿ縺医ｋ繧医≧縺ｫ)
                    TempData["PagingInfo"] = pagingInfoJson;
                }
            }
        }
    }
}
