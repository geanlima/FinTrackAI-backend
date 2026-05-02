using System.Diagnostics;
using System.Text;
using FinTrackAI.Domain.Interfaces.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FinTrackAI.Infrastructure.Services;

public sealed class ManutencaoService : IManutencaoService
{
    private const long TamanhoMaximoBytes = 52_428_800;

    private static readonly byte[] CabecalhoSqlite = "SQLite format 3\0"u8.ToArray();

    private static readonly string[] OrdemLimpeza =
    {
        "monitoramento_precos_ofertas_historico",
        "monitoramento_precos_ofertas",
        "monitoramento_precos",
        "planejamentos_despesa_itens",
        "planejamentos_despesa",
        "pessoas_me_devem",
        "metricas_alertas_disparados",
        "metricas_limites",
        "lembretes",
        "investimento_bluminers_rentabilidade",
        "investimento_bluminers_movimentos",
        "investimento_bluminers_config",
        "investimento_cdi_rendimentos",
        "investimento_cdi_movimentos",
        "investimento_cdi_config",
        "investimento_carteiras",
        "integracao_faturas_cache_itens",
        "integracao_faturas_cache",
        "fatura_cartao_lancamento",
        "fatura_cartao",
        "despesas_fixas",
        "conta_pagar",
        "lancamentos",
        "destinos_renda",
        "fontes_renda",
        "categorias_subcategorias",
        "subcategorias_personalizadas",
        "categorias_personalizadas",
        "cartao_credito_calendario",
        "cartao_credito",
        "conta_bancaria",
        "usuarios",
    };

    private static readonly string[] OrdemMigracao =
    {
        "usuarios",
        "conta_bancaria",
        "cartao_credito",
        "cartao_credito_calendario",
        "categorias_personalizadas",
        "subcategorias_personalizadas",
        "categorias_subcategorias",
        "fontes_renda",
        "destinos_renda",
        "lancamentos",
        "conta_pagar",
        "despesas_fixas",
        "fatura_cartao",
        "fatura_cartao_lancamento",
        "integracao_faturas_cache",
        "integracao_faturas_cache_itens",
        "investimento_carteiras",
        "investimento_cdi_config",
        "investimento_cdi_movimentos",
        "investimento_cdi_rendimentos",
        "investimento_bluminers_config",
        "investimento_bluminers_movimentos",
        "investimento_bluminers_rentabilidade",
        "lembretes",
        "metricas_limites",
        "metricas_alertas_disparados",
        "pessoas_me_devem",
        "planejamentos_despesa",
        "planejamentos_despesa_itens",
        "monitoramento_precos",
        "monitoramento_precos_ofertas",
        "monitoramento_precos_ofertas_historico",
    };

    private static readonly HashSet<string> TabelasPermitidas =
        new HashSet<string>(OrdemLimpeza.Concat(OrdemMigracao), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// SQLite permite NULL onde o PostgreSQL tem NOT NULL DEFAULT. INSERT explícito com NULL não aplica o DEFAULT.
    /// Chave: "tabela|coluna" (case-insensitive).
    /// </summary>
    private static readonly Dictionary<string, object> SubstituirNullNaMigracao = CriarMapaSubstituirNullNaMigracao();

    private readonly IConfiguration _configuration;
    private readonly ILogger<ManutencaoService> _logger;
    private readonly ImportacaoState _importacaoState;

    public ManutencaoService(
        IConfiguration configuration,
        ILogger<ManutencaoService> logger,
        ImportacaoState importacaoState)
    {
        _configuration = configuration;
        _logger = logger;
        _importacaoState = importacaoState;
    }

    private static Dictionary<string, object> CriarMapaSubstituirNullNaMigracao()
    {
        Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        void Def(string tabela, string coluna, object valor) => d[$"{tabela}|{coluna}"] = valor;

        foreach (string c in new[] { "banco", "agencia", "numero", "tipo" })
        {
            Def("conta_bancaria", c, "");
        }

        Def("cartao_credito", "foto_path", "");
        Def("cartao_credito", "dia_vencimento", 0);
        Def("cartao_credito", "tipo", 0);
        Def("cartao_credito", "permite_parcelamento", 1);
        Def("cartao_credito", "controla_fatura", 1);
        Def("cartao_credito", "limite", 0.0);
        Def("cartao_credito", "dia_fechamento", 0);
        Def("cartao_credito", "codigo_cartao_api", "");

        Def("categorias_personalizadas", "cor", "");

        Def("fontes_renda", "dia_previsto", 0);

        Def("lancamentos", "data_pagamento", 0L);
        Def("lancamentos", "grupo_parcelas", "");
        Def("lancamentos", "parcela_numero", 0);
        Def("lancamentos", "parcela_total", 0);
        Def("lancamentos", "id_cartao", 0);
        Def("lancamentos", "id_conta", 0);
        Def("lancamentos", "id_categoria_personalizada", 0);
        Def("lancamentos", "id_subcategoria_personalizada", 0);

        Def("conta_pagar", "data_pagamento", 0L);
        Def("conta_pagar", "parcela_numero", 0);
        Def("conta_pagar", "parcela_total", 0);
        Def("conta_pagar", "forma_pagamento", 0);
        Def("conta_pagar", "id_cartao", 0);
        Def("conta_pagar", "id_conta", 0);
        Def("conta_pagar", "id_lancamento", 0);
        Def("conta_pagar", "data_cabecalho", 0L);

        Def("despesas_fixas", "forma_pagamento", 0);

        Def("fatura_cartao", "data_pagamento", 0L);

        Def("integracao_faturas_cache", "codigo_cartao_api", "");
        Def("integracao_faturas_cache", "fatura_api_id", "");
        Def("integracao_faturas_cache", "descricao", "");
        Def("integracao_faturas_cache", "data_vencimento", 0L);
        Def("integracao_faturas_cache", "data_fechamento", 0L);
        Def("integracao_faturas_cache", "pago", 0);
        Def("integracao_faturas_cache", "fechada_em", 0L);
        Def("integracao_faturas_cache", "id_lancamento_fatura", 0);

        Def("integracao_faturas_cache_itens", "item_api_id", "");
        Def("integracao_faturas_cache_itens", "data_hora", 0L);
        Def("integracao_faturas_cache_itens", "categoria", "");
        Def("integracao_faturas_cache_itens", "id_lancamento_local", 0);

        Def("investimento_cdi_config", "id_conta_bancaria", 0);

        Def("investimento_cdi_movimentos", "id_lancamento", 0);

        Def("investimento_cdi_rendimentos", "id_lancamento", 0);

        Def("investimento_bluminers_movimentos", "observacao", "");
        Def("investimento_bluminers_movimentos", "origem", "");
        Def("investimento_bluminers_movimentos", "id_origem", 0);

        Def("lembretes", "descricao", "");

        Def("metricas_limites", "mes", 0);
        Def("metricas_limites", "semana", 0);
        Def("metricas_limites", "id_subcategoria_personalizada", 0);
        Def("metricas_limites", "forma_pagamento", 0);
        Def("metricas_limites", "id_cartao", 0);
        Def("metricas_limites", "id_conta", 0);

        Def("pessoas_me_devem", "observacao", "");
        Def("pessoas_me_devem", "id_cartao", 0);
        Def("pessoas_me_devem", "parcelas_total", 0);
        Def("pessoas_me_devem", "grupo_receitas", "");

        Def("planejamentos_despesa", "local", "");
        Def("planejamentos_despesa", "notas", "");

        Def("planejamentos_despesa_itens", "id_categoria_personalizada", 0);
        Def("planejamentos_despesa_itens", "id_subcategoria_personalizada", 0);
        Def("planejamentos_despesa_itens", "data_referencia", 0L);
        Def("planejamentos_despesa_itens", "id_lancamento", 0);
        Def("planejamentos_despesa_itens", "id_conta_pagar", 0);
        Def("planejamentos_despesa_itens", "data_vinculo_contas_pagar", 0L);
        Def("planejamentos_despesa_itens", "valor_total", 0.0);

        Def("monitoramento_precos", "loja", "");
        Def("monitoramento_precos", "url", "");
        Def("monitoramento_precos", "foto_path", "");

        Def("monitoramento_precos_ofertas", "loja", "");
        Def("monitoramento_precos_ofertas", "url", "");

        return d;
    }

    private static object? AplicarPadraoPostgresParaNull(string tabela, string coluna, object? valor)
    {
        if (valor is not null)
        {
            return valor;
        }

        string chave = $"{tabela}|{coluna}";
        return SubstituirNullNaMigracao.TryGetValue(chave, out object? padrao) ? padrao : null;
    }

    /// <summary>
    /// Resolve caminho do SQLite. Se <c>Manutencao:DiretorioSqlitePadrao</c> estiver definido e o valor
    /// informado não for caminho absoluto (ex.: só o nome do arquivo após "Buscar arquivo" no navegador),
    /// combina com essa pasta — evita <c>/app/arquivo.db</c> por causa do diretório de trabalho em Docker.
    /// </summary>
    private bool TryNormalizarCaminhoSqlite(string caminho, out string caminhoCompleto, out string? erro)
    {
        erro = null;
        caminhoCompleto = string.Empty;
        string t = caminho.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(t))
        {
            erro = "Caminho não informado.";
            return false;
        }

        if (!OperatingSystem.IsWindows() && EhCaminhoAbsolutoWindows(t))
        {
            erro =
                "Caminho no formato Windows (C:\\...) não é acessível quando a API roda em Linux ou Docker. " +
                "Monte a pasta do arquivo .db no container (volume), defina Manutencao:DiretorioSqlitePadrao " +
                "com o caminho interno (ex.: /data/banco) ou informe o caminho completo dentro do container.";
            return false;
        }

        string? basePadrao = _configuration["Manutencao:DiretorioSqlitePadrao"]?.Trim();
        if (!string.IsNullOrEmpty(basePadrao) && !Path.IsPathRooted(t))
        {
            caminhoCompleto = Path.GetFullPath(Path.Combine(basePadrao, t));
            _logger.LogDebug("SQLite: caminho relativo {Relativo} resolvido com DiretorioSqlitePadrao -> {Completo}", t, caminhoCompleto);
            return true;
        }

        caminhoCompleto = Path.GetFullPath(t);
        return true;
    }

    private static bool EhCaminhoAbsolutoWindows(string t) =>
        t.Length >= 3
        && char.IsAsciiLetter(t[0])
        && t[1] == ':'
        && (t[2] == '\\' || t[2] == '/');

    public async Task<ConexaoStatusDto> TestarConexaoSqliteAsync(string caminho)
    {
        if (!TryNormalizarCaminhoSqlite(caminho, out string caminhoCompleto, out string? erroNormalizacao))
        {
            return new ConexaoStatusDto(false, erroNormalizacao ?? "Caminho inválido.");
        }
        if (!File.Exists(caminhoCompleto))
        {
            return new ConexaoStatusDto(false, $"Arquivo não encontrado: {caminhoCompleto}");
        }

        FileInfo info = new FileInfo(caminhoCompleto);
        if (info.Length > TamanhoMaximoBytes)
        {
            return new ConexaoStatusDto(false, "Arquivo excede o limite de 50 MB.");
        }

        string ext = Path.GetExtension(caminhoCompleto);
        if (!ext.Equals(".db", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return new ConexaoStatusDto(false, "Extensão inválida. Use .db ou .sqlite.");
        }

        byte[] bytes = new byte[16];
        await using (FileStream fs = new FileStream(
                           caminhoCompleto,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           bufferSize: 4096,
                           useAsync: true))
        {
            int lido = await fs.ReadAsync(bytes.AsMemory(0, bytes.Length));
            if (lido < 16)
            {
                return new ConexaoStatusDto(false, "Arquivo não é um banco SQLite válido");
            }
        }

        string magic = Encoding.ASCII.GetString(bytes, 0, 15);
        if (magic != "SQLite format 3")
        {
            return new ConexaoStatusDto(false, "Arquivo não é um banco SQLite válido");
        }

        List<string> tabelas = new List<string>();
        await using SqliteConnection conn = new SqliteConnection($"Data Source={caminhoCompleto};Mode=ReadOnly");
        await conn.OpenAsync();
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
        await using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tabelas.Add(reader.GetString(0));
        }

        return new ConexaoStatusDto(
            true,
            $"Conectado com sucesso — {tabelas.Count} tabelas encontradas",
            new Dictionary<string, object>
            {
                ["tabelas"] = tabelas,
                ["totalTabelas"] = tabelas.Count,
            });
    }

    public async Task<ConexaoStatusDto> TestarConexaoPostgresAsync(CancellationToken cancellationToken = default)
    {
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new ConexaoStatusDto(false, "ConnectionStrings:DefaultConnection não configurada.");
        }

        try
        {
            await using NpgsqlConnection conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await using (NpgsqlCommand cmd = new NpgsqlCommand("SELECT 1", conn))
            {
                await cmd.ExecuteScalarAsync(cancellationToken);
            }

            int totalLancamentos;
            await using (NpgsqlCommand cmdCount = new NpgsqlCommand(
                             "SELECT COUNT(*) FROM lancamentos",
                             conn))
            {
                object? n = await cmdCount.ExecuteScalarAsync(cancellationToken);
                totalLancamentos = n is long l ? (int)l : Convert.ToInt32(n, System.Globalization.CultureInfo.InvariantCulture);
            }

            return new ConexaoStatusDto(
                true,
                $"PostgreSQL acessível — {totalLancamentos} lançamento(s) no banco.",
                new Dictionary<string, object>
                {
                    ["totalLancamentos"] = totalLancamentos,
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao testar PostgreSQL");
            return new ConexaoStatusDto(false, $"Erro: {ex.Message}");
        }
    }

    public async Task<IntegracaoResultadoDto> ExecutarIntegracaoAsync(
        string caminhoSqlite,
        Func<LogEventoDto, Task> onLog,
        CancellationToken cancellationToken = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Dictionary<string, int> registrosPorTabela = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<string> erros = new List<string>();

        await onLog(new LogEventoDto("info", "Validando banco SQLite..."));
        ConexaoStatusDto statusSqlite = await TestarConexaoSqliteAsync(caminhoSqlite);
        if (!statusSqlite.Conectado)
        {
            await onLog(new LogEventoDto("erro", statusSqlite.Mensagem));
            sw.Stop();
            return new IntegracaoResultadoDto(
                false,
                0,
                0,
                registrosPorTabela,
                new List<string> { statusSqlite.Mensagem },
                sw.Elapsed.TotalSeconds);
        }

        await onLog(new LogEventoDto("sucesso", statusSqlite.Mensagem));

        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            const string msg = "Connection string DefaultConnection não configurada.";
            await onLog(new LogEventoDto("erro", msg));
            sw.Stop();
            erros.Add(msg);
            return new IntegracaoResultadoDto(
                false,
                0,
                0,
                registrosPorTabela,
                erros,
                sw.Elapsed.TotalSeconds);
        }

        await onLog(new LogEventoDto("info", "Conectando ao PostgreSQL..."));

        if (!TryNormalizarCaminhoSqlite(caminhoSqlite, out string caminhoCompleto, out string? erroNorm))
        {
            await onLog(new LogEventoDto("erro", erroNorm ?? "Caminho SQLite inválido."));
            sw.Stop();
            erros.Add(erroNorm ?? "Caminho inválido");
            return new IntegracaoResultadoDto(
                false,
                0,
                0,
                registrosPorTabela,
                erros,
                sw.Elapsed.TotalSeconds);
        }

        try
        {
            await using NpgsqlConnection pg = new NpgsqlConnection(connectionString);
            await pg.OpenAsync(cancellationToken);
            await onLog(new LogEventoDto("sucesso", "PostgreSQL conectado"));

            await onLog(new LogEventoDto("aviso", "Limpando dados anteriores..."));
            await LimparPostgresAsync(pg, onLog, cancellationToken);
            await onLog(new LogEventoDto("sucesso", "Dados anteriores removidos"));

            await onLog(new LogEventoDto("info", $"Iniciando migração de {OrdemMigracao.Length} tabelas..."));

            await using SqliteConnection sqliteConn = new SqliteConnection($"Data Source={caminhoCompleto};Mode=ReadOnly");
            await sqliteConn.OpenAsync(cancellationToken);

            foreach (string tabela in OrdemMigracao)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TabelasPermitidas.Contains(tabela))
                {
                    continue;
                }

                if (!await TabelaExisteNoSqliteAsync(sqliteConn, tabela, cancellationToken))
                {
                    await onLog(
                        new LogEventoDto(
                            "info",
                            $"Tabela ausente no SQLite — ignorada: {tabela}",
                            Tabela: tabela));
                    continue;
                }

                try
                {
                    int count = await MigrarTabelaAsync(sqliteConn, pg, tabela, cancellationToken);
                    registrosPorTabela[tabela] = count;
                    await onLog(
                        new LogEventoDto(
                            "sucesso",
                            "Tabela migrada com sucesso",
                            Tabela: tabela,
                            Registros: count));
                }
                catch (Exception ex)
                {
                    string msg = $"Erro na tabela {tabela}: {ex.Message}";
                    erros.Add(msg);
                    _logger.LogError(ex, "Erro ao migrar {Tabela}", tabela);
                    await onLog(new LogEventoDto("erro", msg, Tabela: tabela));
                }
            }

            await onLog(new LogEventoDto("info", "Atualizando sequences do PostgreSQL..."));
            await AtualizarSequencesAsync(pg, cancellationToken);
            await onLog(new LogEventoDto("sucesso", "Sequences atualizadas"));

            NpgsqlConnection.ClearPool(pg);

            sw.Stop();
            int total = registrosPorTabela.Values.Sum();
            bool sucesso = erros.Count == 0;
            await onLog(
                new LogEventoDto(
                    "sucesso",
                    $"Integração concluída! {total} registros em {sw.Elapsed.TotalSeconds:F1}s"));

            if (sucesso)
            {
                _importacaoState.RegistrarImportacaoConcluida();
            }

            return new IntegracaoResultadoDto(
                sucesso,
                total,
                registrosPorTabela.Count,
                registrosPorTabela,
                erros,
                sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Falha global na integração");
            erros.Add(ex.Message);
            await onLog(new LogEventoDto("erro", ex.Message));
            return new IntegracaoResultadoDto(
                false,
                registrosPorTabela.Values.Sum(),
                registrosPorTabela.Count,
                registrosPorTabela,
                erros,
                sw.Elapsed.TotalSeconds);
        }
    }

    private async Task LimparPostgresAsync(
        NpgsqlConnection pg,
        Func<LogEventoDto, Task> onLog,
        CancellationToken cancellationToken)
    {
        foreach (string tabela in OrdemLimpeza)
        {
            if (!TabelasPermitidas.Contains(tabela))
            {
                continue;
            }

            try
            {
                await using NpgsqlCommand cmd = pg.CreateCommand();
                cmd.CommandText = $"TRUNCATE TABLE {PgQuoteIdent(tabela)} RESTART IDENTITY CASCADE;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("TRUNCATE concluído: {Tabela}", tabela);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TRUNCATE ignorado ou falhou para {Tabela}", tabela);
                await onLog(
                    new LogEventoDto(
                        "aviso",
                        $"TRUNCATE ignorado para {tabela}: {ex.Message}",
                        Tabela: tabela));
            }
        }
    }

    private static async Task<bool> TabelaExisteNoSqliteAsync(
        SqliteConnection sqlite,
        string tabela,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n LIMIT 1;";
        cmd.Parameters.AddWithValue("$n", tabela);
        object? r = await cmd.ExecuteScalarAsync(cancellationToken);
        return r != null;
    }

    private async Task<int> MigrarTabelaAsync(
        SqliteConnection sqlite,
        NpgsqlConnection pg,
        string tabela,
        CancellationToken cancellationToken)
    {
        List<string> colunas = new List<string>();
        await using (SqliteCommand cmdSchema = sqlite.CreateCommand())
        {
            cmdSchema.CommandText = $"PRAGMA table_info({SqliteQuoteIdent(tabela)});";
            await using SqliteDataReader rd = await cmdSchema.ExecuteReaderAsync(cancellationToken);
            while (await rd.ReadAsync(cancellationToken))
            {
                string nome = rd.GetString(1);
                colunas.Add(nome);
            }
        }

        if (colunas.Count == 0)
        {
            return 0;
        }

        int inseridosTotal = 0;
        List<object?[]> lote = new List<object?[]>(100);

        await using SqliteCommand cmdSelect = sqlite.CreateCommand();
        cmdSelect.CommandText = $"SELECT * FROM {SqliteQuoteIdent(tabela)};";
        await using SqliteDataReader reader = await cmdSelect.ExecuteReaderAsync(cancellationToken);
        int fieldCount = reader.FieldCount;
        if (fieldCount != colunas.Count)
        {
            _logger.LogWarning(
                "Colunas PRAGMA ({Pragma}) ≠ SELECT ({Select}) na tabela {Tabela}",
                colunas.Count,
                fieldCount,
                tabela);
        }

        int colCount = Math.Min(colunas.Count, fieldCount);
        List<string> colunasEfetivas = colunas.GetRange(0, colCount);
        string listaColunas = string.Join(", ", colunasEfetivas.Select(PgQuoteIdent));

        while (await reader.ReadAsync(cancellationToken))
        {
            object?[] valores = new object?[colCount];
            for (int i = 0; i < colCount; i++)
            {
                object v = reader.GetValue(i);
                object? bruto = v is DBNull ? null : v;
                valores[i] = AplicarPadraoPostgresParaNull(tabela, colunasEfetivas[i], bruto);
            }

            lote.Add(valores);
            if (lote.Count >= 100)
            {
                inseridosTotal += await InserirLoteAsync(pg, tabela, colunasEfetivas, listaColunas, lote, cancellationToken);
                lote.Clear();
            }
        }

        if (lote.Count > 0)
        {
            inseridosTotal += await InserirLoteAsync(pg, tabela, colunasEfetivas, listaColunas, lote, cancellationToken);
        }

        return inseridosTotal;
    }

    private static async Task<int> InserirLoteAsync(
        NpgsqlConnection pg,
        string tabela,
        List<string> colunas,
        string listaColunasSql,
        List<object?[]> lote,
        CancellationToken cancellationToken)
    {
        if (lote.Count == 0)
        {
            return 0;
        }

        StringBuilder sql = new StringBuilder(256 + lote.Count * colunas.Count * 8);
        sql.Append("INSERT INTO ").Append(PgQuoteIdent(tabela)).Append(" (").Append(listaColunasSql).Append(") VALUES ");
        List<NpgsqlParameter> parametros = new List<NpgsqlParameter>();
        int pi = 0;
        int numCols = colunas.Count;
        for (int r = 0; r < lote.Count; r++)
        {
            if (r > 0)
            {
                sql.Append(',');
            }

            sql.Append('(');
            object?[] linha = lote[r];
            for (int c = 0; c < numCols; c++)
            {
                if (c > 0)
                {
                    sql.Append(',');
                }

                string nome = "p" + pi++;
                sql.Append('@').Append(nome);
                object? valor = c < linha.Length ? linha[c] : null;
                parametros.Add(new NpgsqlParameter(nome, valor ?? DBNull.Value));
            }

            sql.Append(')');
        }

        await using NpgsqlCommand cmd = new NpgsqlCommand(sql.ToString(), pg);
        cmd.Parameters.AddRange(parametros.ToArray());
        int afetados = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return afetados;
    }

    private async Task AtualizarSequencesAsync(NpgsqlConnection pg, CancellationToken cancellationToken)
    {
        foreach (string tabela in OrdemMigracao)
        {
            if (!TabelasPermitidas.Contains(tabela))
            {
                continue;
            }

            try
            {
                await using NpgsqlCommand cmd = pg.CreateCommand();
                cmd.CommandText =
                    "SELECT setval(pg_get_serial_sequence(@tbl, 'id'), COALESCE((SELECT MAX(id) FROM "
                    + PgQuoteIdent(tabela)
                    + "), 1), true);";
                cmd.Parameters.AddWithValue("tbl", "public." + tabela);
                await cmd.ExecuteScalarAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Sequence não atualizada para {Tabela} (sem serial id?)", tabela);
            }
        }
    }

    private static string PgQuoteIdent(string ident)
    {
        return "\"" + ident.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string SqliteQuoteIdent(string ident)
    {
        return "\"" + ident.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
