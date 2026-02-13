using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using GeneralAffairsManagementProject.Utils;

namespace GeneralAffairsManagementProject.Pages
{
    public class NewOrderModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<NewOrderModel> _logger;

        public NewOrderModel(IConfiguration configuration, ILogger<NewOrderModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // 画面から受け取るID
        [BindProperty]
        [Required(ErrorMessage = "発注方法を選択してください。")]
        public int? OrderingMethodId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "カテゴリを選択してください。")]
        public int? CategoryId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "品目を選択してください。")]
        public int? ConsumablesId { get; set; }

        [BindProperty]
        [Range(1, 99, ErrorMessage = "数量は 1～99 の範囲で入力してください。")]
        public int Quantity { get; set; }

        // プルダウン候補
        public List<OrderingMethodDto> OrderingMethods { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();
        public List<ConsumablesDto> Consumables { get; set; } = new();

        public void OnGet()
        {
            // リクエスト相関用の情報（AIのoperation_Idとは別に、アプリ側で追いたい時用）
            var correlationId = HttpContext.TraceIdentifier;
            var userName = User.Identity?.Name ?? "anonymous";

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnGet",
                ["CorrelationId"] = correlationId,
                ["UserName"] = userName
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("新規発注画面 初期表示開始");

                try
                {
                    LoadOrderingMethods();
                    _logger.LogInformation("発注方法候補ロード完了 count={Count}", OrderingMethods.Count);

                    if (OrderingMethodId.HasValue)
                    {
                        Categories = LoadCategories(OrderingMethodId.Value);
                        _logger.LogInformation("カテゴリ候補ロード完了 methodId={MethodId} count={Count}",
                            OrderingMethodId.Value, Categories.Count);
                    }

                    if (CategoryId.HasValue)
                    {
                        Consumables = LoadConsumables(CategoryId.Value);
                        _logger.LogInformation("品目候補ロード完了 categoryId={CategoryId} count={Count}",
                            CategoryId.Value, Consumables.Count);
                    }

                    _logger.LogInformation("新規発注画面 初期表示終了 elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "新規発注画面 初期表示中に例外発生 elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    throw; // 画面初期表示で落ちるなら例外としても把握できるようにする
                }
            }
        }

        // Ajax: 発注方法IDからカテゴリ一覧を返す
        public JsonResult OnGetCategories(int methodId)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userName = User.Identity?.Name ?? "anonymous";

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnGetCategories",
                ["CorrelationId"] = correlationId,
                ["UserName"] = userName,
                ["MethodId"] = methodId
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("カテゴリ取得(Ajax)開始");

                try
                {
                    var list = LoadCategories(methodId);
                    _logger.LogInformation("カテゴリ取得(Ajax)成功 count={Count} elapsedMs={ElapsedMs}",
                        list.Count, sw.ElapsedMilliseconds);
                    return new JsonResult(list);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "カテゴリ取得(Ajax)失敗 elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    // クライアントに詳細を返しすぎない
                    Response.StatusCode = 500;
                    return new JsonResult(new { error = "カテゴリ取得に失敗しました。" });
                }
            }
        }

        // Ajax: カテゴリIDから品目一覧を返す
        public JsonResult OnGetConsumables(int categoryId)
        {
            var correlationId = HttpContext.TraceIdentifier;
            var userName = User.Identity?.Name ?? "anonymous";

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnGetConsumables",
                ["CorrelationId"] = correlationId,
                ["UserName"] = userName,
                ["CategoryId"] = categoryId
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("品目取得(Ajax)開始");

                try
                {
                    var list = LoadConsumables(categoryId);
                    _logger.LogInformation("品目取得(Ajax)成功 count={Count} elapsedMs={ElapsedMs}",
                        list.Count, sw.ElapsedMilliseconds);
                    return new JsonResult(list);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "品目取得(Ajax)失敗 elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    Response.StatusCode = 500;
                    return new JsonResult(new { error = "品目取得に失敗しました。" });
                }
            }
        }

        public IActionResult OnPost()
        {
            var correlationId = HttpContext.TraceIdentifier;
            var orderUserName = User.Identity?.Name ?? "system";
            var orderNo = DateTime.Now.ToString("yyyyMMddHHmmss");
            var jstNow = DateTimeUtils.GetJstNow();

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                ["Handler"] = "OnPost",
                ["CorrelationId"] = correlationId,
                ["OrderNo"] = orderNo,
                ["UserName"] = orderUserName,
                ["OrderingMethodId"] = OrderingMethodId,
                ["CategoryId"] = CategoryId,
                ["ConsumablesId"] = ConsumablesId,
                ["Quantity"] = Quantity
            }))
            {
                var sw = Stopwatch.StartNew();
                _logger.LogInformation("新規発注登録開始");

                if (!ModelState.IsValid)
                {
                    // 入力不備はエラーではなく Warning で十分
                    _logger.LogWarning("入力バリデーションNG。画面再表示。");
                    ReloadForPage();
                    return Page();
                }

                string connStr = _configuration.GetConnectionString("GeneralAffairsDb");

                try
                {
                    using var conn = new SqlConnection(connStr);
                    conn.Open();
                    _logger.LogInformation("DB接続成功");

                    using var tran = conn.BeginTransaction();
                    _logger.LogInformation("トランザクション開始");

                    // 1) 整合性チェック
                    _logger.LogInformation("整合性チェック開始");
                    using (var cmd = new SqlCommand(@"
SELECT COUNT(1)
FROM TM_CONSUMABLES s
JOIN TM_CONSUMABLES_CATEGORY c ON c.ID = s.CATEGORY_ID
WHERE s.ID = @ConsumablesId
  AND s.DELETE_FLAG = 0
  AND c.ID = @CategoryId
  AND c.METHOD_ID = @MethodId
  AND c.DELETE_FLAG = 0;
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@ConsumablesId", ConsumablesId!.Value);
                        cmd.Parameters.AddWithValue("@CategoryId", CategoryId!.Value);
                        cmd.Parameters.AddWithValue("@MethodId", OrderingMethodId!.Value);

                        var ok = (int)cmd.ExecuteScalar() == 1;
                        if (!ok)
                        {
                            _logger.LogWarning("整合性チェックNG（組み合わせ不整合）。ロールバックします。");
                            tran.Rollback();
                            ModelState.AddModelError(string.Empty, "選択内容に不整合があります（発注方法・カテゴリ・品目の組み合わせを確認してください）。");
                            ReloadForPage();
                            return Page();
                        }
                    }
                    _logger.LogInformation("整合性チェックOK");

                    // 2) 単価取得
                    _logger.LogInformation("単価取得開始");
                    int unitPrice;
                    using (var cmd = new SqlCommand(@"
SELECT UNIT_PRICE
FROM TM_CONSUMABLES
WHERE ID = @ConsumablesId
  AND DELETE_FLAG = 0;
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@ConsumablesId", ConsumablesId!.Value);
                        object? result = cmd.ExecuteScalar();
                        if (result == null)
                        {
                            _logger.LogWarning("単価取得NG（品目が存在しない）。ロールバックします。");
                            tran.Rollback();
                            ModelState.AddModelError(string.Empty, "品目が存在しません。");
                            ReloadForPage();
                            return Page();
                        }
                        unitPrice = Convert.ToInt32(result);
                    }
                    _logger.LogInformation("単価取得OK unitPrice={UnitPrice}", unitPrice);

                    // 3) 親テーブル登録
                    _logger.LogInformation("親テーブル登録開始");
                    int newOrderId;
                    using (var cmd = new SqlCommand(@"
INSERT INTO TD_ORDER
    (ORDER_NO,
     ORDER_DATE,
     CREATE_DATETIME,
     UPDATE_DATETIME,
     ORDER_USER_NAME,
     CONTACT_ORDER_NAME,
     ORDER_NOTE,
     ORDER_STATUS_ID,
     DELETE_FLAG)
OUTPUT INSERTED.ID
VALUES
    (@OrderNo,
     @OrderDate,
     @CreateDateTime,
     @UpdateDateTime,
     @OrderUserName,
     @ContactOrderName,
     NULL,
     1,
     0);
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@OrderNo", orderNo);
                        cmd.Parameters.AddWithValue("@OrderDate", jstNow);
                        cmd.Parameters.AddWithValue("@CreateDateTime", jstNow);
                        cmd.Parameters.AddWithValue("@UpdateDateTime", jstNow);
                        cmd.Parameters.AddWithValue("@OrderUserName", orderUserName);
                        cmd.Parameters.AddWithValue("@ContactOrderName", orderUserName);

                        newOrderId = (int)cmd.ExecuteScalar();
                    }
                    _logger.LogInformation("親テーブル登録OK newOrderId={NewOrderId}", newOrderId);

                    // 4) 明細登録
                    _logger.LogInformation("明細登録開始");
                    using (var cmd = new SqlCommand(@"
INSERT INTO TD_ORDER_DETAILS
    (ORDER_ID,
     SEQ_NO,
     CONSUMABLES_ID,
     ORDER_QUANTITY,
     ORDER_UNIT_NAME,
     ORDERING_TIME_UNIT_PRICE,
     SCHEDULED_DELIVERY_DATE,
     DELIVERYED_FLAG,
     ORDER_DETAILS_NOTE,
     DELETE_FLAG,
     CREATE_DATETIME,
     UPDATE_DATETIME)
VALUES
    (@OrderId,
     1,
     @ConsumablesId,
     @OrderQuantity,
     @OrderUnitName,
     @UnitPrice,
     NULL,
     0,
     NULL,
     0,
     @CreateDateTime,
     @UpdateDateTime);
", conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", newOrderId);
                        cmd.Parameters.AddWithValue("@ConsumablesId", ConsumablesId!.Value);
                        cmd.Parameters.AddWithValue("@OrderQuantity", Quantity);
                        cmd.Parameters.AddWithValue("@OrderUnitName", "NoData");
                        cmd.Parameters.AddWithValue("@UnitPrice", unitPrice);
                        cmd.Parameters.AddWithValue("@CreateDateTime", jstNow);
                        cmd.Parameters.AddWithValue("@UpdateDateTime", jstNow);

                        cmd.ExecuteNonQuery();
                    }
                    _logger.LogInformation("明細登録OK");

                    // 5) コミット
                    tran.Commit();
                    _logger.LogInformation("トランザクションCommit完了 elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);

                    return RedirectToPage("/Complete");
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "SQL例外（DB接続/クエリ/トランザクション） elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    ModelState.AddModelError(string.Empty, "データベースエラーが発生しました。");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "想定外例外（新規発注登録） elapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                    ModelState.AddModelError(string.Empty, "登録中にエラーが発生しました。");
                }

                // 例外時のみ画面再表示
                ReloadForPage();
                return Page();
            }
        }

        public IActionResult OnPostCancel()
        {
            _logger.LogInformation("新規発注キャンセル");
            return RedirectToPage("/NewOrder");
        }

        private void ReloadForPage()
        {
            LoadOrderingMethods();
            if (OrderingMethodId.HasValue) Categories = LoadCategories(OrderingMethodId.Value);
            if (CategoryId.HasValue) Consumables = LoadConsumables(CategoryId.Value);
        }

        private void LoadOrderingMethods()
        {
            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");
            using var conn = new SqlConnection(connStr);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT ID, NAME
FROM TM_ORDERING_METHOD
WHERE DELETE_FLAG = 0
ORDER BY ID;
", conn);

            using var reader = cmd.ExecuteReader();
            OrderingMethods.Clear();
            while (reader.Read())
            {
                OrderingMethods.Add(new OrderingMethodDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }

        private List<CategoryDto> LoadCategories(int methodId)
        {
            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");
            using var conn = new SqlConnection(connStr);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT ID, CATEGORY_NAME
FROM TM_CONSUMABLES_CATEGORY
WHERE METHOD_ID = @MethodId
  AND DELETE_FLAG = 0
ORDER BY ID;
", conn);

            cmd.Parameters.AddWithValue("@MethodId", methodId);

            using var reader = cmd.ExecuteReader();
            var list = new List<CategoryDto>();
            while (reader.Read())
            {
                list.Add(new CategoryDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
            return list;
        }

        private List<ConsumablesDto> LoadConsumables(int categoryId)
        {
            string connStr = _configuration.GetConnectionString("GeneralAffairsDb");
            using var conn = new SqlConnection(connStr);
            conn.Open();

            using var cmd = new SqlCommand(@"
SELECT ID, ITEM_NAME
FROM TM_CONSUMABLES
WHERE CATEGORY_ID = @CategoryId
  AND DELETE_FLAG = 0
ORDER BY ID;
", conn);

            cmd.Parameters.AddWithValue("@CategoryId", categoryId);

            using var reader = cmd.ExecuteReader();
            var list = new List<ConsumablesDto>();
            while (reader.Read())
            {
                list.Add(new ConsumablesDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
            return list;
        }

        public class OrderingMethodDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class CategoryDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class ConsumablesDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
