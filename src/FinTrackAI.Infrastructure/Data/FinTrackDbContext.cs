using FinTrackAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinTrackAI.Infrastructure.Data;

public sealed class FinTrackDbContext : DbContext
{
    public FinTrackDbContext(DbContextOptions<FinTrackDbContext> options)
        : base(options)
    {
    }

    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    public DbSet<Categoria> CategoriasPersonalizadas => Set<Categoria>();

    public DbSet<Subcategoria> SubcategoriasPersonalizadas => Set<Subcategoria>();

    public DbSet<ContaBancaria> ContasBancarias => Set<ContaBancaria>();

    public DbSet<CartaoCredito> CartoesCredito => Set<CartaoCredito>();

    public DbSet<ContaPagar> ContasPagar => Set<ContaPagar>();

    public DbSet<FaturaCartao> FaturasCartao => Set<FaturaCartao>();

    public DbSet<DespesaFixa> DespesasFixas => Set<DespesaFixa>();

    public DbSet<FonteRenda> FontesRenda => Set<FonteRenda>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lancamento>(entity =>
        {
            entity.ToTable("lancamentos");
            entity.HasKey(e => e.Id);
            entity.Ignore(e => e.DataHoraFormatada);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Valor).HasColumnName("valor");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.FormaPagamento).HasColumnName("forma_pagamento").IsRequired(false);
            entity.Property(e => e.DataHora).HasColumnName("data_hora");
            entity.Property(e => e.PagamentoFatura).HasColumnName("pagamento_fatura");
            entity.Property(e => e.Pago).HasColumnName("pago");
            entity.Property(e => e.DataPagamento).HasColumnName("data_pagamento").IsRequired(false);
            entity.Property(e => e.Categoria).HasColumnName("categoria");
            entity.Property(e => e.GrupoParcelas).HasColumnName("grupo_parcelas");
            entity.Property(e => e.ParcelaNumero).HasColumnName("parcela_numero").IsRequired(false);
            entity.Property(e => e.ParcelaTotal).HasColumnName("parcela_total").IsRequired(false);
            entity.Property(e => e.IdCartao).HasColumnName("id_cartao").IsRequired(false);
            entity.Property(e => e.IdConta).HasColumnName("id_conta").IsRequired(false);
            entity.Property(e => e.TipoMovimento).HasColumnName("tipo_movimento").IsRequired(false);
            entity.Property(e => e.IdCategoriaPersonalizada).HasColumnName("id_categoria_personalizada").IsRequired(false);
            entity.Property(e => e.TipoDespesa).HasColumnName("tipo_despesa").IsRequired(false);
            entity.Property(e => e.IdSubcategoriaPersonalizada).HasColumnName("id_subcategoria_personalizada").IsRequired(false);
        });

        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("categorias_personalizadas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.TipoMovimento).HasColumnName("tipo_movimento").IsRequired(false);
            entity.Property(e => e.Cor).HasColumnName("cor").IsRequired(false);
        });

        modelBuilder.Entity<Subcategoria>(entity =>
        {
            entity.ToTable("subcategorias_personalizadas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdCategoriaPersonalizada).HasColumnName("id_categoria_personalizada");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
        });

        modelBuilder.Entity<ContaBancaria>(entity =>
        {
            entity.ToTable("conta_bancaria");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.Banco).HasColumnName("banco");
            entity.Property(e => e.Agencia).HasColumnName("agencia");
            entity.Property(e => e.Numero).HasColumnName("numero");
            entity.Property(e => e.Tipo).HasColumnName("tipo");
            entity.Property(e => e.Ativa).HasColumnName("ativa");
        });

        modelBuilder.Entity<CartaoCredito>(entity =>
        {
            entity.ToTable("cartao_credito");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.Bandeira).HasColumnName("bandeira");
            entity.Property(e => e.Ultimos4).HasColumnName("ultimos4");
            entity.Property(e => e.DiaVencimento).HasColumnName("dia_vencimento");
            entity.Property(e => e.Limite).HasColumnName("limite");
            entity.Property(e => e.DiaFechamento).HasColumnName("dia_fechamento");
        });

        modelBuilder.Entity<ContaPagar>(entity =>
        {
            entity.ToTable("conta_pagar");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.Valor).HasColumnName("valor");
            entity.Property(e => e.DataVencimento).HasColumnName("data_vencimento");
            entity.Property(e => e.Pago).HasColumnName("pago");
            entity.Property(e => e.DataPagamento).HasColumnName("data_pagamento");
            entity.Property(e => e.ParcelaNumero).HasColumnName("parcela_numero");
            entity.Property(e => e.ParcelaTotal).HasColumnName("parcela_total");
            entity.Property(e => e.GrupoParcelas).HasColumnName("grupo_parcelas");
            entity.Property(e => e.FormaPagamento).HasColumnName("forma_pagamento");
            entity.Property(e => e.IdCartao).HasColumnName("id_cartao");
            entity.Property(e => e.IdConta).HasColumnName("id_conta");
            entity.Property(e => e.IdLancamento).HasColumnName("id_lancamento");
            entity.Property(e => e.DataCabecalho).HasColumnName("data_cabecalho");
        });

        modelBuilder.Entity<FaturaCartao>(entity =>
        {
            entity.ToTable("fatura_cartao");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IdCartao).HasColumnName("id_cartao");
            entity.Property(e => e.Ano).HasColumnName("ano");
            entity.Property(e => e.Mes).HasColumnName("mes");
            entity.Property(e => e.DataFechamento).HasColumnName("data_fechamento");
            entity.Property(e => e.DataVencimento).HasColumnName("data_vencimento");
            entity.Property(e => e.ValorTotal).HasColumnName("valor_total");
            entity.Property(e => e.Pago).HasColumnName("pago");
            entity.Property(e => e.DataPagamento).HasColumnName("data_pagamento");
        });

        modelBuilder.Entity<DespesaFixa>(entity =>
        {
            entity.ToTable("despesas_fixas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.Valor).HasColumnName("valor");
            entity.Property(e => e.DiaVencimento).HasColumnName("dia_vencimento");
            entity.Property(e => e.FormaPagamento).HasColumnName("forma_pagamento");
            entity.Property(e => e.Ativo).HasColumnName("ativo");
            entity.Property(e => e.GerarAutomatico).HasColumnName("gerar_automatico");
            entity.Property(e => e.CriadoEm).HasColumnName("criado_em");
        });

        modelBuilder.Entity<FonteRenda>(entity =>
        {
            entity.ToTable("fontes_renda");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.ValorBase).HasColumnName("valor_base");
            entity.Property(e => e.Fixa).HasColumnName("fixa");
            entity.Property(e => e.DiaPrevisto).HasColumnName("dia_previsto");
            entity.Property(e => e.Ativa).HasColumnName("ativa");
            entity.Property(e => e.IncluirNaRendaDiaria).HasColumnName("incluir_na_renda_diaria");
        });
    }
}
