using System.Data;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Application Insights の追加
builder.Services.AddApplicationInsightsTelemetry();

// Add services to the container.
builder.Services.AddRazorPages();

// ★ DB接続を DI に登録（appsettings.json優先、なければ App Service の環境変数を使用）
builder.Services.AddScoped<IDbConnection>(_ =>
{
    var cs = builder.Configuration.GetConnectionString("GeneralAffairsDb")
             ?? Environment.GetEnvironmentVariable("SQLCONNSTR_GeneralAffairsDb");
    return new SqlConnection(cs);
});

// ★ 追加：Authorization（Razorで User を使う/ [Authorize] を使うなら入れておく）
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ★ 追加：Easy Auth のヘッダーを読み取って HttpContext.User を作る（MapRazorPagesより前）
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var principalHeader))
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(principalHeader!));
            using var doc = JsonDocument.Parse(json);

            var claims = new List<Claim>();

            // userDetails（存在すれば）
            if (doc.RootElement.TryGetProperty("userDetails", out var userDetails))
            {
                var ud = userDetails.GetString();
                if (!string.IsNullOrEmpty(ud))
                    claims.Add(new Claim(ClaimTypes.Name, ud));
            }

            // claims 配列（typ/val）
            if (doc.RootElement.TryGetProperty("claims", out var claimsArray)
                && claimsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in claimsArray.EnumerateArray())
                {
                    var type = c.GetProperty("typ").GetString();
                    var val = c.GetProperty("val").GetString();
                    if (!string.IsNullOrEmpty(type) && val != null)
                        claims.Add(new Claim(type, val));
                }
            }

            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "EasyAuth"));
        }
        catch
        {
            // 壊れてたら未認証扱いのまま
        }
    }

    await next();
});

// 既存
app.UseAuthorization();

app.MapRazorPages();

app.Run();
