using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RzekaReporting.Functions.Database;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// App setting "SqlConnection": local.settings.json locally, App Settings in Azure.
builder.Services.AddDbContext<ReportsDbContext>(options =>
    options.UseSqlServer(builder.Configuration["SqlConnection"])
);
builder.Services.AddScoped<ReportStore>();

builder.Build().Run();
