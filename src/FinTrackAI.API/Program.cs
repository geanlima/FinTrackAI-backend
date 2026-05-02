using Anthropic.SDK;
using FinTrackAI.Application.Services;
using FinTrackAI.Application.UseCases.Categorias;
using FinTrackAI.Application.UseCases.Chat;
using FinTrackAI.Application.UseCases.ContasPagar;
using FinTrackAI.Application.UseCases.Lancamentos;
using FinTrackAI.Domain.Interfaces.Repositories;
using FinTrackAI.Domain.Interfaces.Services;
using FinTrackAI.Infrastructure.Configuration;
using FinTrackAI.Infrastructure.Data;
using FinTrackAI.Infrastructure.Repositories;
using FinTrackAI.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ANTHROPIC_API_KEY (comum em PaaS) não mapeia sozinho para a seção Anthropic:ApiKey.
if (string.IsNullOrWhiteSpace(builder.Configuration["Anthropic:ApiKey"]))
{
    string? deAmbiente = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    if (!string.IsNullOrWhiteSpace(deAmbiente))
        builder.Configuration["Anthropic:ApiKey"] = deAmbiente;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800;
});

string[]? configuredOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
string[] allowedOrigins = configuredOrigins is { Length: > 0 }
    ? configuredOrigins
    : ["http://localhost"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "FinTrackCors",
        policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

builder.Services.Configure<AnthropicSettings>(
    builder.Configuration.GetSection(AnthropicSettings.SectionName));

string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddSingleton<DataSourceState>();
builder.Services.AddDbContext<FinTrackDbContext>(
    (sp, options) =>
    {
        DataSourceState fonte = sp.GetRequiredService<DataSourceState>();
        string? sqlite = fonte.CaminhoSqliteAtivo;
        if (!string.IsNullOrWhiteSpace(sqlite))
        {
            SqliteConnectionStringBuilder sqliteCs = new() { DataSource = sqlite };
            options.UseSqlite(sqliteCs.ConnectionString);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:DefaultConnection ou defina um SQLite com POST /api/Manutencao/fonte-dados.");
            options.UseNpgsql(connectionString);
        }
    });

builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();
builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<IContaPagarRepository, ContaPagarRepository>();

builder.Services.AddScoped<FinanceiroQueryService>();

builder.Services.AddScoped<AnthropicClient>(sp =>
{
    AnthropicSettings settings = sp.GetRequiredService<IOptions<AnthropicSettings>>().Value;
    AnthropicApiKeyState keyState = sp.GetRequiredService<AnthropicApiKeyState>();
    string key = keyState.OverrideKey ?? settings.ApiKey ?? string.Empty;
    return string.IsNullOrWhiteSpace(key)
        ? new AnthropicClient()
        : new AnthropicClient(key);
});

builder.Services.AddScoped<IAgentService, AgentService>();

builder.Services.AddSingleton<ImportacaoState>();
builder.Services.AddSingleton<AnthropicApiKeyState>();
builder.Services.AddScoped<IManutencaoService, ManutencaoService>();

builder.Services.AddScoped<GetLancamentosByPeriodoUseCase>();
builder.Services.AddScoped<GetResumoMensalUseCase>();
builder.Services.AddScoped<GetGastosPorCategoriaUseCase>();
builder.Services.AddScoped<GetCategoriasUseCase>();
builder.Services.AddScoped<GetContasPagarPendentesUseCase>();
builder.Services.AddScoped<EnviarMensagemUseCase>();

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("FinTrackCors");

app.MapControllers();

app.Run();
