# dinheiro

Quantias em **centavos inteiros** do zero em C# e .NET 8 — os seis modos de
arredondamento, e o rateio que fecha.

```
$ dotnet medidor.dll rateio

  resposta                 partes                        soma      fecha?
  ----------------------------------------------------------------------------
  33,33 para cada      R$ 33,33  R$ 33,33  R$ 33,33       R$ 99,99   NÃO, falta 1 centavo
  33,34 para cada      R$ 33,34  R$ 33,34  R$ 33,34      R$ 100,02   NÃO, sobram 2 centavos
  o rateio             R$ 33,34  R$ 33,33  R$ 33,33      R$ 100,00   sim
```

## O juiz é uma invariante, e é exaustivo

A soma das partes é **exatamente** o todo. Isso não é uma comparação com outra
implementação: é uma **dedução sobre o que a operação significa**. Repartir uma
quantia e não obter a quantia de volta não é um arredondamento ruim — é outra
operação.

E é verificável sem amostra: **todas as quantias de −1.000 a +1.000 centavos,
repartidas em todos os números de partes de 1 a 50**. São 100.050 rateios, e em
nenhum pode faltar ou sobrar um centavo. Mais uma segunda invariante que quase
ninguém escreve: nenhuma parte difere de outra por mais de um centavo — repartir
"igualmente" e dar a um o dobro do outro fecharia a conta e estaria errado.

O segundo juiz é o `decimal` do .NET, que representa decimais exatamente e é a
resposta certa contra a qual a soma em centavos é conferida. E o terceiro é o
`Math.Round`, nos dois modos em que ele existe — é por isso que o arredondamento
aqui é escrito à mão em vez de delegar: **uma implementação que delegasse não
teria juiz nenhum**.

São 26 testes.

## Por que não `double`

```
$ dotnet medidor.dll double

  0.1 + 0.2 == 0.3   →   False
  0.1 + 0.2          =    0.30000000000000004
  o double de 0.1    =    0.10000000000000001

       quantas        em double          em centavos         erro
  ----------------------------------------------------------------------
         1,000         505687.18          505687.18       0.000000
        10,000        5014903.08         5014903.08       0.000000
       100,000       49968582.67        49968582.67      -0.000001
     1,000,000      499716667.46       499716667.46      -0.000014
```

Vale ser honesto sobre a escala: com um milhão de valores sorteados o erro ainda
**não chegou a um centavo** — está na casa de 0,00001. Eu tinha escrito no
programa que já aparecia nos centavos, e a própria medida me desmentiu.

Mesmo assim, três motivos que bastam:

1. **o erro depende da ordem das parcelas** — somar os mesmos lançamentos em ordem
   diferente dá totais diferentes, e dois relatórios do mesmo dia deixam de bater
   entre si;
2. **a comparação quebra antes da soma** — `0.1 + 0.2 == 0.3` é falso, e um
   `if (saldo == 0)` pode não disparar num saldo que é zero;
3. **com valores grandes o erro cresce junto** — o `double` tem 15 a 16
   algarismos, e um total na casa dos bilhões já não tem centavo confiável.

Em centavos inteiros nada disso existe: a soma é exata em qualquer ordem, em
qualquer escala, e a comparação é comparação de inteiros.

Não é um defeito do C#. É o IEEE 754, e vale igual em Java, Python, JavaScript e
C: 0,1 não existe em binário, do mesmo jeito que 1/3 não existe em decimal.

## Os seis modos, e o único sem viés

```
$ dotnet medidor.dll vies

  modo             soma arredondada      soma exata          erro
  --------------------------------------------------------------------
  para o par           5,000,000,000     5,000,000,000             0    ← sem viés
  longe do zero        5,000,050,000     5,000,000,000        50,000
  para o zero          4,999,950,000     5,000,000,000       -50,000
  para cima            5,000,050,000     5,000,000,000        50,000
  para baixo           4,999,950,000     5,000,000,000       -50,000
  truncar              4,999,950,000     5,000,000,000       -50,000
```

Cem mil empates exatos. **"Para o par" erra zero** — não "quase zero": zero.
Metade dos empates sobe e metade desce, e eles se cancelam exatamente. É o padrão
do IEEE 754, do `Math.Round` e da norma contábil de quase todo lugar.

"Longe do zero", que é o que se aprende na escola, erra meio por empate — num
milhão de linhas, R$ 5.000,00. A legislação tributária brasileira costuma exigir
justamente esse, e é uma decisão política, não técnica: a lei ganha da
estatística e o sistema obedece.

Todo arredondamento é trivial menos **um** caso — o empate exato. Fora dele, os
quatro modos de vizinho concordam, e é por isso que a escolha parece não importar
até somar um milhão de vezes.

E há uma armadilha: **`Truncar` e `ParaBaixo` só divergem nos negativos**. Um
teste que usa apenas valores positivos não vê a diferença, e é aí que nasce o
defeito de estorno — cobrar com um modo e devolver com o outro devolve um centavo
a mais.

## O rateio por pesos

```
    um desconto de R$ 0,01 entre dois itens de 70% e 30%:
      R$ 0,01 e R$ 0,00  =  R$ 0,01

    R$ 1.000,00 de frete entre pedidos de 3, 5 e 7 kg:
      R$ 200,00, R$ 333,34 e R$ 466,66  =  R$ 1.000,00
```

Quem **perde mais na divisão** recebe o centavo que sobra — é o método do maior
resto, o mesmo de Hamilton na repartição de cadeiras. O desempate é pelo índice,
e isso não é detalhe: sem ele, a ordenação de restos empatados dependeria do
algoritmo de ordenação, e duas execuções poderiam dar resultados diferentes. Um
teste exige que 5.000 rateios sorteados deem exatamente o mesmo em três execuções.

(O método de Hamilton tem o **paradoxo do Alabama** — aumentar o total pode fazer
alguém *perder* uma parte. Ele existe aqui também e não importa: ninguém reparte a
mesma nota fiscal duas vezes com totais diferentes.)

## A decisão que a assinatura obriga

Não existe conversão de `decimal` para quantia sem modo de arredondamento, e não
existe operador `*` para taxa:

```csharp
Valor.De(1.005m, Arredondamento.ParaOPar)      // 1,00
Valor.De(1.005m, Arredondamento.ParaLongeDoZero)  // 1,01

valor.Vezes(0.0825m, Arredondamento.ParaOPar)  // e não valor * 0.0825m
```

Uma sobrecarga sem modo escolheria em silêncio. Um arredondamento escolhido em
silêncio, repetido um milhão de vezes, é a diferença entre fechar e não fechar o
caixa — então a decisão está na assinatura, onde quem chama tropeça nela.

## A velocidade

```
  representação       tempo        razão    exata?
  ----------------------------------------------------
  long de centavos      7.1 ms      1.0x    sim
  decimal             402.2 ms     56.3x    sim
  double               20.5 ms      2.9x    NÃO
```

O `decimal` é exato e serve — é o que a maioria usa, e é o juiz desta suíte. O que
ele custa é **56 vezes** o tempo, e o que ele arrisca é que dividir dois `decimal`
devolve um `decimal` com casas que ninguém pediu.

O `double` é mais rápido que o `decimal` e é o único errado. A velocidade dele não
compra nada: um total que não fecha não vale mais rápido.

## Rodar

.NET 8. Zero dependências fora do xUnit, e só nos testes.

```
dotnet test testes/Dinheiro.Testes/Dinheiro.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- rateio
```

As medidas de tempo moram na ferramenta e **não** na suíte, de propósito: uma
afirmação de relógio falha sem que nada tenha piorado — basta a máquina do CI
estar ocupada — e o efeito é ensinar quem trabalha no projeto a ignorar teste
vermelho.

## O que ele não faz

**Não tem moeda.** Toda quantia aqui é um número, e somar reais com dólares
compila. Um sistema de verdade precisa do tipo carregar a moeda e recusar a soma
— e precisa da ISO 4217, que sabe que o iene não tem centavo e o dinar do Kuwait
tem três casas.

Não faz câmbio, nem tem taxa com data. Não tem juros, nem tabela Price, nem
correção monetária. Não tem imposto: a regra do ICMS por estado, do ISS por
município e da retenção por faixa é um projeto inteiro, e um que muda toda lei
orçamentária.

Não guarda em banco, e portanto não resolve o problema real de um sistema
financeiro, que é a **conciliação** — garantir que o que foi calculado é o que foi
gravado, e que ninguém gravou duas vezes.

Não tem quantia com mais de duas casas, que é o que um sistema de combustível ou
de energia precisa (o litro de gasolina custa R$ 5,899).

## Licença

MIT.
