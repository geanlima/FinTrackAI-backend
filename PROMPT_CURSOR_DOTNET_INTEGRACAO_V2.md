# PROMPT CURSOR — Microsserviço Python — Agentes FinTrack AI

## CONTEXTO

O FinTrack AI já tem uma API .NET Web API funcionando com Clean Architecture.
Preciso de um **microsserviço Python** pequeno que roda na porta 8001
e expõe apenas um endpoint: `POST /agentes/chat`

O .NET chama esse endpoint internamente. O Angular não sabe que existe.

---

## RESPONSABILIDADE DESTE SERVIÇO

Apenas processar mensagens via agentes LangGraph especializados.
Sem autenticação, sem CRUD, sem banco próprio além de memória do agente.

---

## STACK

- Python 3.12
- FastAPI + Uvicorn (porta 8001)
- LangGraph + LangChain Anthropic
- psycopg2 para ler PostgreSQL existente (porta 5434)
- httpx para Serper API
- python-dotenv

---

## BANCO DE DADOS — PostgreSQL existente (porta 5434, banco: fintrack)

**Nunca escrever nas tabelas de dados — somente leitura.**
Criar tabelas próprias de memória do agente no mesmo banco.

### Schema completo das tabelas relevantes:

```sql
-- Categorias
categorias_personalizadas (
    id SERIAL PRIMARY KEY,
    nome TEXT,
    tipo_movimento INTEGER,  -- 1=despesa, 2=receita
    cor TEXT
)

-- Subcategorias — vinculadas à categoria
subcategorias_personalizadas (
    id SERIAL PRIMARY KEY,
    id_categoria_personalizada INTEGER REFERENCES categorias_personalizadas(id),
    nome TEXT,
    criado_em BIGINT
)

-- Tabela de relação categoria ↔ subcategoria
categorias_subcategorias (
    id SERIAL PRIMARY KEY,
    id_categoria_personalizada INTEGER REFERENCES categorias_personalizadas(id),
    id_subcategoria_personalizada INTEGER REFERENCES subcategorias_personalizadas(id),
    criado_em BIGINT
)

-- Lançamentos financeiros
lancamentos (
    id SERIAL PRIMARY KEY,
    descricao TEXT,
    valor REAL,
    data_hora BIGINT,                    -- timestamp Unix em milissegundos
    tipo_movimento INTEGER,              -- 1=despesa, 2=receita
    pago INTEGER,                        -- 0=não pago, 1=pago
    id_categoria_personalizada INTEGER NOT NULL DEFAULT 0,
    id_subcategoria_personalizada INTEGER NOT NULL DEFAULT 0
)

-- Contas a pagar
conta_pagar (
    id SERIAL PRIMARY KEY,
    descricao TEXT,
    valor REAL,
    data_vencimento BIGINT,             -- timestamp Unix em milissegundos
    pago INTEGER
)

-- Investimentos
investimento_carteiras (id SERIAL PRIMARY KEY, nome TEXT)

investimento_cdi_movimentos (
    id SERIAL PRIMARY KEY,
    id_carteira INTEGER,
    tipo INTEGER,    -- 1=entrada, 2=saída
    valor REAL,
    data BIGINT
)
```

### Conversão de datas:
```python
from datetime import datetime
from calendar import monthrange

def periodo_mes(mes: int, ano: int) -> tuple[int, int]:
    """Retorna (inicio_ms, fim_ms) do mês em milissegundos."""
    inicio = int(datetime(ano, mes, 1).timestamp() * 1000)
    _, ultimo_dia = monthrange(ano, mes)
    fim = int(datetime(ano, mes, ultimo_dia, 23, 59, 59).timestamp() * 1000)
    return inicio, fim

def ts_para_data(ts_ms: int) -> str:
    if not ts_ms:
        return ""
    return datetime.fromtimestamp(ts_ms / 1000).strftime("%d/%m/%Y")
```

### Tabelas de memória (criar se não existir):
```sql
CREATE TABLE IF NOT EXISTS agente_sessoes (
    id SERIAL PRIMARY KEY,
    usuario_id TEXT NOT NULL,
    resumo TEXT NOT NULL,
    criado_em TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS agente_fatos (
    id SERIAL PRIMARY KEY,
    usuario_id TEXT NOT NULL,
    chave TEXT NOT NULL,
    valor TEXT NOT NULL,
    atualizado_em TIMESTAMP DEFAULT NOW(),
    UNIQUE(usuario_id, chave)
);
```

---

## ESTRUTURA DO PROJETO

```
vox-finance-ia-agentes/
├── main.py
├── .env
├── requirements.txt
├── agents/
│   ├── supervisor.py
│   ├── gastos_agent.py
│   ├── investimentos_agent.py
│   ├── precos_agent.py
│   └── compra_agent.py
├── tools/
│   ├── db_tools.py        ← consultas PostgreSQL somente leitura
│   ├── search_tools.py    ← Serper API
│   └── memory_tools.py    ← memória do agente
└── core/
    ├── config.py
    └── database.py
```

---

## .env

```
ANTHROPIC_API_KEY=sua_chave
SERPER_API_KEY=sua_chave_serper
POSTGRES_URL=postgresql://postgres:postgres@localhost:5434/fintrack
```

---

## requirements.txt

```
fastapi==0.115.0
uvicorn==0.30.6
langgraph==0.2.50
langchain-anthropic==0.3.0
anthropic==0.49.0
psycopg2-binary==2.9.9
python-dotenv==1.0.1
httpx==0.27.0
pydantic==2.9.0
```

---

## FERRAMENTAS DO BANCO (db_tools.py)

Implementar todas usando psycopg2.
Retornam sempre strings JSON para o agente consumir.

```python
import psycopg2
import json
import os
from datetime import datetime
from calendar import monthrange

POSTGRES_URL = os.environ.get("POSTGRES_URL")

def get_conn():
    return psycopg2.connect(POSTGRES_URL)


def buscar_resumo_mensal(mes: int, ano: int) -> str:
    """Total de receitas, despesas e saldo do período."""
    inicio, fim = periodo_mes(mes, ano)
    with get_conn() as conn:
        cur = conn.cursor()
        cur.execute("""
            SELECT
                COALESCE(SUM(CASE WHEN tipo_movimento = 2 THEN valor ELSE 0 END), 0) as receitas,
                COALESCE(SUM(CASE WHEN tipo_movimento = 1 THEN valor ELSE 0 END), 0) as despesas,
                COUNT(*) as total_lancamentos
            FROM lancamentos
            WHERE data_hora BETWEEN %s AND %s
              AND pago = 1
        """, (inicio, fim))
        row = cur.fetchone()
        receitas  = float(row[0])
        despesas  = float(row[1])
        return json.dumps({
            "mes": mes, "ano": ano,
            "receitas":          receitas,
            "despesas":          despesas,
            "saldo":             receitas - despesas,
            "total_lancamentos": row[2]
        })


def buscar_gastos_por_categoria(mes: int, ano: int) -> str:
    """
    Gastos agrupados por categoria COM subcategorias detalhadas.

    Usa JOIN com as três tabelas:
    lancamentos → categorias_personalizadas → subcategorias_personalizadas

    Retorna hierarquia completa:
    [
      {
        "categoria": "Alimentação",
        "total": 850.00,
        "subcategorias": [
          {"nome": "Supermercado", "total": 600.00},
          {"nome": "Restaurante",  "total": 250.00}
        ]
      }
    ]
    """
    inicio, fim = periodo_mes(mes, ano)
    with get_conn() as conn:
        cur = conn.cursor()

        # Busca gastos com categoria E subcategoria via JOIN
        cur.execute("""
            SELECT
                COALESCE(c.nome, 'Sem categoria')  AS categoria,
                COALESCE(s.nome, 'Sem subcategoria') AS subcategoria,
                SUM(l.valor)                        AS total
            FROM lancamentos l
            LEFT JOIN categorias_personalizadas c
                ON l.id_categoria_personalizada = c.id
                AND l.id_categoria_personalizada > 0
            LEFT JOIN subcategorias_personalizadas s
                ON l.id_subcategoria_personalizada = s.id
                AND l.id_subcategoria_personalizada > 0
            WHERE l.data_hora BETWEEN %s AND %s
              AND l.tipo_movimento = 1
              AND l.pago = 1
            GROUP BY c.nome, s.nome
            ORDER BY SUM(l.valor) DESC
        """, (inicio, fim))
        rows = cur.fetchall()

    # Agrupa subcategorias dentro de cada categoria
    categorias: dict = {}
    for categoria, subcategoria, total in rows:
        if categoria not in categorias:
            categorias[categoria] = {"categoria": categoria,
                                     "total": 0.0,
                                     "subcategorias": []}
        categorias[categoria]["total"] += float(total)
        if subcategoria and subcategoria != "Sem subcategoria":
            categorias[categoria]["subcategorias"].append({
                "nome":  subcategoria,
                "total": float(total)
            })

    # Ordena subcategorias por valor
    resultado = sorted(categorias.values(),
                       key=lambda x: x["total"], reverse=True)
    for cat in resultado:
        cat["subcategorias"].sort(key=lambda x: x["total"], reverse=True)

    return json.dumps(resultado, ensure_ascii=False)


def buscar_lancamentos_recentes(mes: int, ano: int, limite: int = 10) -> str:
    """Últimos lançamentos do período com categoria e subcategoria."""
    inicio, fim = periodo_mes(mes, ano)
    with get_conn() as conn:
        cur = conn.cursor()
        cur.execute("""
            SELECT
                l.descricao,
                l.valor,
                l.data_hora,
                l.tipo_movimento,
                COALESCE(c.nome, 'Sem categoria')    AS categoria,
                COALESCE(s.nome, 'Sem subcategoria') AS subcategoria
            FROM lancamentos l
            LEFT JOIN categorias_personalizadas c
                ON l.id_categoria_personalizada = c.id
                AND l.id_categoria_personalizada > 0
            LEFT JOIN subcategorias_personalizadas s
                ON l.id_subcategoria_personalizada = s.id
                AND l.id_subcategoria_personalizada > 0
            WHERE l.data_hora BETWEEN %s AND %s
              AND l.pago = 1
            ORDER BY l.data_hora DESC
            LIMIT %s
        """, (inicio, fim, limite))
        rows = cur.fetchall()

    resultado = [
        {
            "descricao":    r[0],
            "valor":        float(r[1]),
            "data":         ts_para_data(r[2]),
            "tipo":         "receita" if r[3] == 2 else "despesa",
            "categoria":    r[4],
            "subcategoria": r[5]
        }
        for r in rows
    ]
    return json.dumps(resultado, ensure_ascii=False)


def buscar_contas_pendentes() -> str:
    """Contas a pagar não pagas com dias até vencimento."""
    with get_conn() as conn:
        cur = conn.cursor()
        cur.execute("""
            SELECT descricao, valor, data_vencimento
            FROM conta_pagar
            WHERE pago = 0
            ORDER BY data_vencimento ASC
        """)
        rows = cur.fetchall()

    hoje_ms = int(datetime.now().timestamp() * 1000)
    resultado = []
    for r in rows:
        venc_ms = r[2] or 0
        dias = int((venc_ms - hoje_ms) / (1000 * 60 * 60 * 24))
        resultado.append({
            "descricao":  r[0],
            "valor":      float(r[1]),
            "vencimento": ts_para_data(venc_ms),
            "dias_ate_vencimento": dias,
            "status": "vencida" if dias < 0
                      else "vence hoje" if dias == 0
                      else f"vence em {dias} dias"
        })
    return json.dumps(resultado, ensure_ascii=False)


def buscar_historico_meses(quantidade: int = 6) -> str:
    """Evolução financeira dos últimos N meses."""
    hoje = datetime.now()
    resultado = []
    for i in range(quantidade - 1, -1, -1):
        mes_offset = (hoje.month - 1 - i) % 12 + 1
        ano_offset = hoje.year + ((hoje.month - 1 - i) // 12)
        dados = json.loads(buscar_resumo_mensal(mes_offset, ano_offset))
        resultado.append(dados)
    return json.dumps(resultado, ensure_ascii=False)


def buscar_top_gastos(mes: int, ano: int, limite: int = 5) -> str:
    """Top N maiores gastos do período com categoria e subcategoria."""
    inicio, fim = periodo_mes(mes, ano)
    with get_conn() as conn:
        cur = conn.cursor()
        cur.execute("""
            SELECT
                l.descricao,
                l.valor,
                COALESCE(c.nome, 'Sem categoria')    AS categoria,
                COALESCE(s.nome, 'Sem subcategoria') AS subcategoria,
                l.data_hora
            FROM lancamentos l
            LEFT JOIN categorias_personalizadas c
                ON l.id_categoria_personalizada = c.id
                AND l.id_categoria_personalizada > 0
            LEFT JOIN subcategorias_personalizadas s
                ON l.id_subcategoria_personalizada = s.id
                AND l.id_subcategoria_personalizada > 0
            WHERE l.data_hora BETWEEN %s AND %s
              AND l.tipo_movimento = 1
              AND l.pago = 1
            ORDER BY l.valor DESC
            LIMIT %s
        """, (inicio, fim, limite))
        rows = cur.fetchall()

    resultado = [
        {
            "descricao":    r[0],
            "valor":        float(r[1]),
            "categoria":    r[2],
            "subcategoria": r[3],
            "data":         ts_para_data(r[4])
        }
        for r in rows
    ]
    return json.dumps(resultado, ensure_ascii=False)


def buscar_investimentos() -> str:
    """Carteiras de investimento com saldo calculado."""
    with get_conn() as conn:
        cur = conn.cursor()
        cur.execute("""
            SELECT
                w.nome,
                COALESCE(SUM(
                    CASE WHEN m.tipo = 1 THEN m.valor ELSE -m.valor END
                ), 0) AS saldo,
                COUNT(m.id) AS num_movimentos
            FROM investimento_carteiras w
            LEFT JOIN investimento_cdi_movimentos m ON m.id_carteira = w.id
            GROUP BY w.id, w.nome
            ORDER BY saldo DESC
        """)
        rows = cur.fetchall()

    resultado = [
        {
            "carteira":       r[0],
            "saldo":          float(r[1]),
            "num_movimentos": r[2]
        }
        for r in rows
    ]
    return json.dumps(resultado, ensure_ascii=False)
```

---

## FERRAMENTAS DE BUSCA (search_tools.py)

```python
import httpx
import json
import os

SERPER_KEY = os.environ.get("SERPER_API_KEY")

async def buscar_precos(produto: str) -> str:
    """Busca preços em lojas brasileiras via Serper Shopping."""
    async with httpx.AsyncClient(timeout=10) as client:
        r = await client.post(
            "https://google.serper.dev/shopping",
            headers={"X-API-KEY": SERPER_KEY},
            json={"q": produto, "gl": "br", "hl": "pt-br", "num": 8}
        )
    itens = r.json().get("shopping", [])[:5]
    resultado = [
        {
            "nome":  i.get("title", ""),
            "preco": i.get("price", ""),
            "loja":  i.get("source", ""),
            "link":  i.get("link", "")
        }
        for i in itens
    ]
    return json.dumps(resultado, ensure_ascii=False)

async def buscar_noticias_investimento(tipo: str) -> str:
    """Busca notícias financeiras atuais via Serper."""
    async with httpx.AsyncClient(timeout=10) as client:
        r = await client.post(
            "https://google.serper.dev/news",
            headers={"X-API-KEY": SERPER_KEY},
            json={"q": f"{tipo} investimento brasil 2026",
                  "gl": "br", "hl": "pt-br", "num": 5}
        )
    noticias = r.json().get("news", [])[:4]
    resultado = [
        {
            "titulo": n.get("title", ""),
            "fonte":  n.get("source", ""),
            "data":   n.get("date", ""),
            "resumo": n.get("snippet", "")
        }
        for n in noticias
    ]
    return json.dumps(resultado, ensure_ascii=False)
```

---

## AGENTES LANGGRAPH

### Estado compartilhado:
```python
from typing import TypedDict, Annotated
from langgraph.graph.message import add_messages

class EstadoAgente(TypedDict):
    messages:   Annotated[list, add_messages]
    pergunta:   str
    usuario_id: str
    mes:        int
    ano:        int
    destino:    str   # agente escolhido pelo supervisor
    dados:      str   # resultado das ferramentas
    resposta:   str
```

### Supervisor (supervisor.py):
```python
# Usa claude-haiku-4-5-20251001 para classificar (barato e rápido)
# Categorias:
# - "gastos":        perguntas sobre despesas, categorias, subcategorias
# - "investimentos": dicas, rentabilidade, onde aplicar
# - "precos":        buscar preço de produto específico
# - "compra":        posso comprar X? análise financeira
# - "geral":         saldo, resumo, contas a pagar
```

### Agente de Gastos (gastos_agent.py):
```python
# Ferramentas:
# - buscar_resumo_mensal(mes, ano)
# - buscar_gastos_por_categoria(mes, ano)   ← retorna categorias COM subcategorias
# - buscar_lancamentos_recentes(mes, ano)   ← retorna categoria e subcategoria
# - buscar_top_gastos(mes, ano)             ← retorna categoria e subcategoria
# - buscar_historico_meses(6)
#
# Padrão: Self-RAG — decide quais ferramentas usar baseado na pergunta
#
# IMPORTANTE: ao apresentar gastos por categoria,
# sempre mostrar as subcategorias dentro de cada categoria.
# Exemplo de resposta:
# Alimentação: R$ 850,00
#   • Supermercado: R$ 600,00
#   • Restaurante: R$ 250,00
#
# Formata valores como R$ X.XXX,XX
```

### Agente de Investimentos (investimentos_agent.py):
```python
# Ferramentas:
# - buscar_investimentos()                  ← dados do PostgreSQL
# - buscar_noticias_investimento(tipo)      ← Serper
#
# Padrão: Corrective RAG
# Sempre inclui: "Isso não é recomendação financeira profissional"
```

### Agente de Preços (precos_agent.py):
```python
# Ferramentas:
# - buscar_precos(produto)                  ← Serper Shopping
# - buscar_resumo_mensal(mes, ano)          ← verifica se pode pagar
#
# Extrai nome do produto da pergunta
# Lista top 5 com preço, loja e link
```

### Agente de Compra (compra_agent.py):
```python
# Ferramentas:
# - buscar_resumo_mensal(mes, ano)
# - buscar_contas_pendentes()
# - buscar_gastos_por_categoria(mes, ano)   ← para ver onde pode cortar
# - buscar_precos(produto)                  ← se valor não informado
#
# Padrão: Plan-and-Execute
# Resposta sempre inclui:
# - Saldo atual
# - Contas pendentes
# - Maior categoria de gasto (para sugerir corte se necessário)
# - Recomendação clara: PODE / NÃO PODE / CUIDADO
```

---

## ENDPOINT PRINCIPAL (main.py)

```python
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
import json

app = FastAPI(title="Vox Finance IA — Agentes")

app.add_middleware(CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["POST", "GET"],
    allow_headers=["*"])

class MensagemHistorico(BaseModel):
    role: str
    content: str

class ChatRequest(BaseModel):
    mensagem:   str
    historico:  list[MensagemHistorico] = []
    usuario_id: str = "default"
    mes:        int = None   # padrão: mês atual
    ano:        int = None   # padrão: ano atual

@app.post("/agentes/chat")
async def chat(body: ChatRequest):
    from datetime import datetime
    mes = body.mes or datetime.now().month
    ano = body.ano or datetime.now().year

    async def gerar():
        # Executa o grafo LangGraph com supervisor
        # Emite tokens via SSE conforme gera
        # Formato: f"data: {json.dumps(evento)}\n\n"
        # evento = {"tipo": "token", "conteudo": "..."}
        # evento = {"tipo": "agente", "conteudo": "gastos"}
        # Termina com: "data: [DONE]\n\n"
        pass

    return StreamingResponse(
        gerar(),
        media_type="text/event-stream",
        headers={
            "Cache-Control":    "no-cache",
            "X-Accel-Buffering":"no"
        }
    )

@app.get("/health")
async def health():
    # Testa conexão com PostgreSQL
    # Retorna {"status": "ok", "postgres": true, "total_lancamentos": N}
    pass
```

---

## RESULTADO ESPERADO

Após `python -m uvicorn main:app --reload --port 8001`:

- `GET http://localhost:8001/health` → `{"status": "ok"}`
- `POST http://localhost:8001/agentes/chat` com `{"mensagem": "quanto gastei em alimentação?"}`:
  ```
  Seus gastos em Alimentação este mês foram R$ 850,00:
    • Supermercado: R$ 600,00
    • Restaurante: R$ 250,00
  ```

O .NET vai chamar este endpoint internamente e fazer proxy do SSE para o Angular.