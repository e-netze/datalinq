using DataLinq.Code.Extensions;
using E.DataLinq.Code.Extensions.DependencyInjection;
using E.DataLinq.Code.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(Path.Combine("_config", "datalinq.code.json"), optional: false, reloadOnChange: false);

builder.AddServiceDefaults();

// Add services to the container.

#region DataLinq Code Services

builder.Services.AddDataLinqCodeService(
    config =>
    {
        config.DatalinqInstances = builder
                .Configuration.GetSection("DataLinq.Code:Instances")
                              .Get<DataLinqCodeOptions.DataLinqInstance[]>();

        /*
        config.DatalinqInstances = new DataLinqCodeOptions.DataLinqInstance[]
        {
            new DataLinqCodeOptions.DataLinqInstance()
            {
                Name = "Local",
                Description = "A local datalinq instance for testing and development",
                LoginUrl = $"~/DataLinqAuth?redirect={{0}}",
                LogoutUrl = $"~/DataLinqAuth/Logout?redirect={{0}}",
                CodeApiClientUrl = "~",
            },
            new DataLinqCodeOptions.DataLinqInstance()
            {
                Name = "WebGIS Api",
                Description = "A datalinq instance hosted in a local WebGIS API",
                LoginUrl = $"https://localhost:44341/DataLinqAuth?redirect={{0}}",
                LogoutUrl = $"https://localhost:44341/DataLinqAuth/Logout?redirect={{0}}",
                CodeApiClientUrl = "https://localhost:44341",
            },
        };
        */

        config.UseAppPrefixFilters = true;
    },
    cryptoOptions =>
    {
        cryptoOptions.DefaultPassword =
            builder.Configuration["DataLinq.Code:Crypto:DefaultPasswort"].OrRandomPassword();
        cryptoOptions.HashBytesSalt =
            Convert.FromBase64String(
                builder.Configuration["DataLinq.Code:Crypto:SaltBytes"].OrRandomSaltBase64()
            );
    });

#endregion


builder.Services.AddControllersWithViews();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseDatalinqCodeAuthentication();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}"
    );

app.Run();
