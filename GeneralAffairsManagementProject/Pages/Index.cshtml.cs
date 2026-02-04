using Microsoft.AspNetCore.Mvc;
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
        /// �������
        /// </summary>
        [BindProperty]
        public OrderSearchCondition SearchCondition { get; set; } = new();

        /// <summary>
        /// ��������
        /// </summary>
        public List<OrderSearchResult> SearchResults { get; set; } = new();

        /// <summary>
        /// �y�[�W���O���
        /// </summary>
        public PagingInfo PagingInfo { get; set; } = new();

        /// <summary>
        /// �������@���X�g
        /// </summary>
        public List<OrderingMethod> OrderingMethods { get; set; } = new();

        /// <summary>
        /// �X�e�[�^�X���X�g
        /// </summary>
        public List<OrderStatus> OrderStatuses { get; set; } = new();

        /// <summary>
        /// �o���f�[�V�����G���[���b�Z�[�W
        /// </summary>
        public Dictionary<string, string> ValidationErrors { get; set; } = new();

        /// <summary>
        /// �����\��
        /// </summary>
        public async Task OnGetAsync()
        {
            _logger.LogInformation("Order list initial display started.");
            
            try
            {
                // �}�X�^�f�[�^�擾
                await LoadMasterDataAsync();

                // ��������Ō���
                SearchCondition.IncludeExpiredNotDelivered = false;
                await ExecuteSearchAsync();
                
                // ���񌟍����ʂ�ۑ�
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
        /// �����{�^������
        /// </summary>
        public async Task<IActionResult> OnPostSearchAsync()
        {
            _logger.LogInformation("Search started. Condition: {@SearchCondition}", SearchCondition);
            
            try
            {
                // �}�X�^�f�[�^�擾
                await LoadMasterDataAsync();

                // ���̓`�F�b�N
                if (!ValidateSearchCondition())
                {
                    _logger.LogWarning("Validation error occurred. Errors: {@ValidationErrors}", ValidationErrors);
                    // �o���f�[�V�����G���[���͈ȑO�̌������ʂ𕜌�
                    RestoreSearchResultsFromTempData();
                    return Page();
                }

                // 1�y�[�W�ڂ�\��
                SearchCondition.CurrentPage = 1;
                await ExecuteSearchAsync();

                // ��������ƌ��ʂ�TempData�ɕۑ�
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
        /// �N���A�{�^������
        /// </summary>
        public IActionResult OnPostClear()
        {
            _logger.LogInformation("Clear search condition started.");
            
            try
            {
                // �}�X�^�f�[�^�擾�i�����Łj
                LoadMasterData();

                // TempData���猟�����ʂ𕜌�
                RestoreSearchResultsFromTempData();

                // ���������N���A�i���f���o�C���f�B���O�O�̏�Ԃɖ߂��j
                ModelState.Clear();
                SearchCondition = new OrderSearchCondition
                {
                    IncludeExpiredNotDelivered = false
                };

                // �������ʂ�TempData�ɍĕۑ��i����̂��߂ɕێ��j
                SaveSearchResultsToTempData();
                
                // ��������̓N���A�����̂�TempData����폜
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
        /// �y�[�W���O����
        /// </summary>
        public async Task<IActionResult> OnPostPageAsync(int page)
        {
            _logger.LogInformation("Paging started. Page: {PageNumber}", page);
            
            try
            {
                // �}�X�^�f�[�^�擾
                await LoadMasterDataAsync();

                // TempData���猟������𕜌�
                RestoreSearchConditionFromTempData();

                // �w��y�[�W�Ō���
                SearchCondition.CurrentPage = page;
                await ExecuteSearchAsync();

                // ��������ƌ��ʂ�ĕۑ�
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
        /// �}�X�^�f�[�^�擾�i�����Łj
        /// </summary>
        private void LoadMasterData()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    conn.Open();

                // �������@�}�X�^�擾
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

                // �����X�e�[�^�X�}�X�^�擾
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
        /// �}�X�^�f�[�^�擾
        /// </summary>
        private async Task LoadMasterDataAsync()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // �������@�}�X�^�擾
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

                // �����X�e�[�^�X�}�X�^�擾
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
        /// ���̓`�F�b�N
        /// </summary>
        private bool ValidateSearchCondition()
        {
            ValidationErrors.Clear();
            bool isValid = true;

            // �������`�F�b�N
            if (SearchCondition.OrderDateFrom.HasValue && SearchCondition.OrderDateTo.HasValue)
            {
                if (SearchCondition.OrderDateFrom.Value > SearchCondition.OrderDateTo.Value)
                {
                    ValidationErrors["OrderDate"] = "������(From)�͔�����(To)�ȑO�̓��t��w�肵�Ă��������B";
                    isValid = false;
                }
            }

            // �[�i���`�F�b�N
            if (SearchCondition.DeliveryDateFrom.HasValue && SearchCondition.DeliveryDateTo.HasValue)
            {
                if (SearchCondition.DeliveryDateFrom.Value > SearchCondition.DeliveryDateTo.Value)
                {
                    ValidationErrors["DeliveryDate"] = "�[�i��(From)�͔[�i��(To)�ȑO�̓��t��w�肵�Ă��������B";
                    isValid = false;
                }
            }

            return isValid;
        }

        /// <summary>
        /// �������s
        /// </summary>
        private async Task ExecuteSearchAsync()
        {
            if (_db is SqlConnection conn)
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                // ��������\�z(WHERE��ƃp�����[�^�𐶐�)
                var (whereClause, parameterFactory) = BuildSearchCondition();

                // �������擾
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

                // �f�[�^�擾(�y�[�W���O)
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
        /// ���������\�z
        /// </summary>
        private (string whereClause, Func<List<SqlParameter>> parameterFactory) BuildSearchCondition()
        {
            var whereClauses = new List<string> { "o.DELETE_FLAG = 0" };

            // �p�����[�^�𐶐�����֐���Ԃ�(�Ăяo�����тɐV�����C���X�^���X�𐶐�)
            Func<List<SqlParameter>> createParameters = () =>
            {
                var parameters = new List<SqlParameter>();

                // �������@(���Օi�}�X�^�o�R�Ŕ���)
                if (SearchCondition.OrderingMethodId.HasValue)
                {
                    parameters.Add(new SqlParameter("@MethodId", SearchCondition.OrderingMethodId.Value));
                }

                // ������(�O����v)
                if (!string.IsNullOrWhiteSpace(SearchCondition.OrderUser))
                {
                    parameters.Add(new SqlParameter("@OrderUser", SearchCondition.OrderUser + "%"));
                }

                // �i�ڔԍ�(�O����v)
                if (!string.IsNullOrWhiteSpace(SearchCondition.ItemNumber))
                {
                    parameters.Add(new SqlParameter("@ItemNumber", SearchCondition.ItemNumber + "%"));
                }

                // �X�e�[�^�X
                if (SearchCondition.StatusId.HasValue)
                {
                    parameters.Add(new SqlParameter("@StatusId", SearchCondition.StatusId.Value));
                }

                // ������From
                if (SearchCondition.OrderDateFrom.HasValue)
                {
                    parameters.Add(new SqlParameter("@OrderDateFrom", SearchCondition.OrderDateFrom.Value.Date));
                }

                // ������To
                if (SearchCondition.OrderDateTo.HasValue)
                {
                    parameters.Add(new SqlParameter("@OrderDateTo", SearchCondition.OrderDateTo.Value.Date.AddDays(1)));
                }

                // �[�i��From
                if (SearchCondition.DeliveryDateFrom.HasValue)
                {
                    parameters.Add(new SqlParameter("@DeliveryDateFrom", SearchCondition.DeliveryDateFrom.Value.Date));
                }

                // �[�i��To
                if (SearchCondition.DeliveryDateTo.HasValue)
                {
                    parameters.Add(new SqlParameter("@DeliveryDateTo", SearchCondition.DeliveryDateTo.Value.Date.AddDays(1)));
                }

                return parameters;
            };

            // �������@(���Օi�}�X�^�o�R�Ŕ���)
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

            // ������(�O����v)
            if (!string.IsNullOrWhiteSpace(SearchCondition.OrderUser))
            {
                whereClauses.Add("o.ORDER_USER_NAME LIKE @OrderUser");
            }

            // �i�ڔԍ�(�O����v)
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

            // �X�e�[�^�X
            if (SearchCondition.StatusId.HasValue)
            {
                whereClauses.Add("o.ORDER_STATUS_ID = @StatusId");
            }

            // ������From
            if (SearchCondition.OrderDateFrom.HasValue)
            {
                whereClauses.Add("o.ORDER_DATE >= @OrderDateFrom");
            }

            // ������To
            if (SearchCondition.OrderDateTo.HasValue)
            {
                whereClauses.Add("o.ORDER_DATE < @OrderDateTo");
            }

            // �[�i��From
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

            // �[�i��To
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

            // �L�������؂ꊎ���[�i��܂܂Ȃ��ꍇ�̏��O���
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
        /// ���������TempData�ɕۑ�
        /// </summary>
        private void SaveSearchConditionToTempData()
        {
            TempData["SearchCondition"] = JsonSerializer.Serialize(SearchCondition);
        }

        /// <summary>
        /// TempData���猟������𕜌�
        /// </summary>
        private void RestoreSearchConditionFromTempData()
        {
            if (TempData.ContainsKey("SearchCondition"))
            {
                var searchConditionJson = TempData["SearchCondition"]?.ToString();
                if (!string.IsNullOrEmpty(searchConditionJson))
                {
                    SearchCondition = JsonSerializer.Deserialize<OrderSearchCondition>(searchConditionJson) ?? new();
                    // TempData��ĕۑ�(�����g����悤��)
                    TempData["SearchCondition"] = searchConditionJson;
                }
            }
        }

        /// <summary>
        /// �������ʂ�TempData�ɕۑ�
        /// </summary>
        private void SaveSearchResultsToTempData()
        {
            TempData["SearchResults"] = JsonSerializer.Serialize(SearchResults);
            TempData["PagingInfo"] = JsonSerializer.Serialize(PagingInfo);
        }

        /// <summary>
        /// TempData���猟�����ʂ𕜌�
        /// </summary>
        private void RestoreSearchResultsFromTempData()
        {
            if (TempData.ContainsKey("SearchResults"))
            {
                var searchResultsJson = TempData["SearchResults"]?.ToString();
                if (!string.IsNullOrEmpty(searchResultsJson))
                {
                    SearchResults = JsonSerializer.Deserialize<List<OrderSearchResult>>(searchResultsJson) ?? new();
                    // TempData��ĕۑ�(�����g����悤��)
                    TempData["SearchResults"] = searchResultsJson;
                }
            }

            if (TempData.ContainsKey("PagingInfo"))
            {
                var pagingInfoJson = TempData["PagingInfo"]?.ToString();
                if (!string.IsNullOrEmpty(pagingInfoJson))
                {
                    PagingInfo = JsonSerializer.Deserialize<PagingInfo>(pagingInfoJson) ?? new();
                    // TempData��ĕۑ�(�����g����悤��)
                    TempData["PagingInfo"] = pagingInfoJson;
                }
            }
        }
    }
}
