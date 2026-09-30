namespace Conde.Dinheiro;

/// <summary>
/// Os modos de arredondamento — e por que existem seis e não um.
/// </summary>
/// <remarks>
/// <para>
/// Todo arredondamento é trivial menos <b>um</b> caso: o empate exato, quando a
/// parte que sobra é exatamente meio centavo. É só ali que os modos divergem, e é
/// por isso que a diferença entre eles parece irrelevante até somar um milhão de
/// linhas.
/// </para>
/// </remarks>
public enum Arredondamento
{
    /// <summary>
    /// No empate, para o <b>par</b> — o do banqueiro, e o padrão de fato.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 0,5 vira 0; 1,5 vira 2; 2,5 vira 2; 3,5 vira 4. Parece arbitrário e é o
    /// único modo <b>sem viés</b>: metade dos empates sobe e metade desce, então
    /// a soma de muitos arredondamentos não deriva para lado nenhum.
    /// </para>
    /// <para>
    /// É o padrão do IEEE 754, do <c>Math.Round</c> do .NET e da norma contábil
    /// de quase todo lugar. O nome "do banqueiro" é folclore — não há registro de
    /// banco algum tê-lo inventado.
    /// </para>
    /// </remarks>
    ParaOPar,

    /// <summary>No empate, para longe do zero — o que se aprende na escola.</summary>
    /// <remarks>
    /// 0,5 vira 1; 1,5 vira 2; 2,5 vira 3; −0,5 vira −1. É o mais intuitivo e
    /// tem <b>viés para cima</b> em módulo: somando um milhão de empates, o total
    /// cresce meio centavo por empate. É o que a legislação tributária brasileira
    /// costuma exigir, e a lei ganha da estatística.
    /// </remarks>
    ParaLongeDoZero,

    /// <summary>No empate, para o zero.</summary>
    ParaOZero,

    /// <summary>Sempre para cima — o teto.</summary>
    /// <remarks>
    /// Não olha empate nenhum: 1,01 vira 2. É o que se usa para cobrar tarifa por
    /// faixa, ou para calcular quantas caixas cabem uma remessa.
    /// </remarks>
    ParaCima,

    /// <summary>Sempre para baixo — o piso.</summary>
    /// <remarks>
    /// É o modo dos juros a favor do cliente, e o de quem não quer nunca cobrar a
    /// mais.
    /// </remarks>
    ParaBaixo,

    /// <summary>Corta a parte fracionária — trunca para o zero.</summary>
    /// <remarks>
    /// Diferente de <see cref="ParaBaixo"/> nos negativos: −1,5 truncado dá −1, e
    /// para baixo dá −2. A diferença é invisível em teste que só usa valores
    /// positivos, e é onde nasce o defeito de estorno.
    /// </remarks>
    Truncar,
}

/// <summary>A conta do arredondamento.</summary>
public static class Arredondar
{
    /// <summary>Arredonda um <c>decimal</c> para inteiro, pelo modo pedido.</summary>
    /// <remarks>
    /// <para>
    /// A implementação é feita à mão, e não com o <c>Math.Round</c>, por dois
    /// motivos. O <c>Math.Round</c> não tem os seis modos — faltam
    /// <c>ParaOZero</c> e a distinção entre <c>ParaBaixo</c> e <c>Truncar</c> —
    /// e, mais importante, escrevê-la à mão é o que permite <b>compará-la</b> com
    /// o <c>Math.Round</c> nos modos em que os dois existem.
    /// </para>
    /// <para>
    /// Uma implementação que chamasse o <c>Math.Round</c> não teria juiz nenhum.
    /// </para>
    /// </remarks>
    public static long Para(decimal valor, Arredondamento modo)
    {
        var inteiro = decimal.Truncate(valor);

        var resto = valor - inteiro;

        if (resto == 0)
        {
            return (long)inteiro;
        }

        var negativo = valor < 0;

        var meio = Math.Abs(resto) == 0.5m;

        return modo switch
        {
            Arredondamento.Truncar => (long)inteiro,

            Arredondamento.ParaCima => (long)(negativo ? inteiro : inteiro + 1),

            Arredondamento.ParaBaixo => (long)(negativo ? inteiro - 1 : inteiro),

            Arredondamento.ParaOZero => meio
                ? (long)inteiro
                : Comum(inteiro, resto, negativo),

            Arredondamento.ParaLongeDoZero => meio
                ? (long)(negativo ? inteiro - 1 : inteiro + 1)
                : Comum(inteiro, resto, negativo),

            _ => meio
                ? ParaOPar(inteiro, negativo)
                : Comum(inteiro, resto, negativo),
        };
    }

    /// <summary>Fora do empate, todos os modos concordam: vai para o mais perto.</summary>
    private static long Comum(decimal inteiro, decimal resto, bool negativo) =>
        Math.Abs(resto) > 0.5m
            ? (long)(negativo ? inteiro - 1 : inteiro + 1)
            : (long)inteiro;

    /// <summary>No empate, para o vizinho par.</summary>
    private static long ParaOPar(decimal inteiro, bool negativo)
    {
        var comoLong = (long)inteiro;

        if (comoLong % 2 == 0)
        {
            return comoLong;
        }

        return negativo ? comoLong - 1 : comoLong + 1;
    }

    /// <summary>O nome do modo, para a mensagem e para a tabela.</summary>
    public static string Nome(Arredondamento modo) => modo switch
    {
        Arredondamento.ParaOPar => "para o par",
        Arredondamento.ParaLongeDoZero => "longe do zero",
        Arredondamento.ParaOZero => "para o zero",
        Arredondamento.ParaCima => "para cima",
        Arredondamento.ParaBaixo => "para baixo",
        _ => "truncar",
    };

    /// <summary>Todos os modos, para percorrer.</summary>
    public static IReadOnlyList<Arredondamento> Todos { get; } =
    [
        Arredondamento.ParaOPar,
        Arredondamento.ParaLongeDoZero,
        Arredondamento.ParaOZero,
        Arredondamento.ParaCima,
        Arredondamento.ParaBaixo,
        Arredondamento.Truncar,
    ];
}
