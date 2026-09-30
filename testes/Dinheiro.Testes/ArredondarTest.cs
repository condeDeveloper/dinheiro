using Xunit;

namespace Conde.Dinheiro.Testes;

/// <summary>
/// O arredondamento — contra o <c>Math.Round</c>, e contra si mesmo.
/// </summary>
/// <remarks>
/// <para>
/// O juiz aqui é o <c>Math.Round</c> do .NET, nos dois modos em que ele existe:
/// <c>MidpointRounding.ToEven</c> e <c>AwayFromZero</c>. É por isso que a conta
/// deste projeto é escrita à mão em vez de chamá-lo — uma implementação que
/// delegasse não teria juiz nenhum.
/// </para>
/// <para>
/// E há dois modos que o <c>Math.Round</c> <b>não</b> tem — <c>ParaOZero</c> e a
/// distinção entre <c>ParaBaixo</c> e <c>Truncar</c> —, e para esses o juiz é
/// outro: a própria definição, conferida em todos os meios centavos de um
/// intervalo.
/// </para>
/// </remarks>
public class ArredondarTest
{
    [Fact(DisplayName = "contra o Math.Round, em 400 mil valores de um quarto em um quarto")]
    public void ContraOMathRound()
    {
        // Passo de 0,25 para que os empates exatos -- que são o único caso em
        // que os modos divergem -- apareçam em metade das amostras.
        var quantos = 0;

        for (var quartos = -200_000; quartos <= 200_000; quartos++)
        {
            var valor = quartos * 0.25m;

            Assert.Equal(
                (long)Math.Round(valor, MidpointRounding.ToEven),
                Arredondar.Para(valor, Arredondamento.ParaOPar));

            Assert.Equal(
                (long)Math.Round(valor, MidpointRounding.AwayFromZero),
                Arredondar.Para(valor, Arredondamento.ParaLongeDoZero));

            // E os que o Math.Round tem sob outro nome.
            Assert.Equal(
                (long)Math.Ceiling(valor),
                Arredondar.Para(valor, Arredondamento.ParaCima));

            Assert.Equal(
                (long)Math.Floor(valor),
                Arredondar.Para(valor, Arredondamento.ParaBaixo));

            Assert.Equal(
                (long)decimal.Truncate(valor),
                Arredondar.Para(valor, Arredondamento.Truncar));

            quantos++;
        }

        Assert.Equal(400_001, quantos);
    }

    [Theory(DisplayName = "o empate, que é o único caso em que os modos divergem")]
    // valor, par, longe do zero, para o zero, cima, baixo, truncar
    [InlineData(0.5, 0, 1, 0, 1, 0, 0)]
    [InlineData(1.5, 2, 2, 1, 2, 1, 1)]
    [InlineData(2.5, 2, 3, 2, 3, 2, 2)]
    [InlineData(3.5, 4, 4, 3, 4, 3, 3)]
    [InlineData(-0.5, 0, -1, 0, 0, -1, 0)]
    [InlineData(-1.5, -2, -2, -1, -1, -2, -1)]
    [InlineData(-2.5, -2, -3, -2, -2, -3, -2)]
    public void OEmpate(
        double valor, long par, long longe, long zero, long cima, long baixo, long truncar)
    {
        var comoDecimal = (decimal)valor;

        Assert.Equal(par, Arredondar.Para(comoDecimal, Arredondamento.ParaOPar));
        Assert.Equal(longe, Arredondar.Para(comoDecimal, Arredondamento.ParaLongeDoZero));
        Assert.Equal(zero, Arredondar.Para(comoDecimal, Arredondamento.ParaOZero));
        Assert.Equal(cima, Arredondar.Para(comoDecimal, Arredondamento.ParaCima));
        Assert.Equal(baixo, Arredondar.Para(comoDecimal, Arredondamento.ParaBaixo));
        Assert.Equal(truncar, Arredondar.Para(comoDecimal, Arredondamento.Truncar));
    }

    [Fact(DisplayName = "fora do empate, os quatro modos de vizinho concordam")]
    public void ForaDoEmpate()
    {
        // É a propriedade que explica por que a escolha de modo parece não
        // importar: em 99% das entradas reais ela não muda nada. Só que o 1%
        // restante, somado um milhão de vezes, é o que não fecha.
        var sorteio = new Random(29);

        for (var caso = 0; caso < 200_000; caso++)
        {
            // Um valor com três casas, que quase nunca cai no meio exato.
            var valor = sorteio.Next(-1_000_000, 1_000_000) / 1000m;

            if (Math.Abs(valor - decimal.Truncate(valor)) == 0.5m)
            {
                continue;
            }

            var par = Arredondar.Para(valor, Arredondamento.ParaOPar);

            Assert.Equal(par, Arredondar.Para(valor, Arredondamento.ParaLongeDoZero));
            Assert.Equal(par, Arredondar.Para(valor, Arredondamento.ParaOZero));
        }
    }

    [Fact(DisplayName = "o viés: somar um milhão de empates mostra a diferença")]
    public void OVies()
    {
        // O modo "para o par" é o único sem viés: metade dos empates sobe e
        // metade desce. O "longe do zero" sobe sempre, e a diferença é meio
        // centavo POR EMPATE.
        //
        // Aqui são cem mil empates, de 0,5 a 99999,5.
        long somaPar = 0;
        long somaLonge = 0;

        decimal somaExata = 0;

        for (var i = 0; i < 100_000; i++)
        {
            var valor = i + 0.5m;

            somaPar += Arredondar.Para(valor, Arredondamento.ParaOPar);
            somaLonge += Arredondar.Para(valor, Arredondamento.ParaLongeDoZero);

            somaExata += valor;
        }

        // O "longe do zero" errou exatamente meio por empate: 50.000 a mais.
        Assert.Equal(50_000, somaLonge - (long)somaExata);

        // E o "para o par" errou zero. Não "quase zero": zero.
        Assert.Equal(0, somaPar - (long)somaExata);
    }

    [Fact(DisplayName = "truncar e para baixo divergem SÓ nos negativos")]
    public void TruncarEParaBaixo()
    {
        // É a distinção que passa despercebida em teste que só usa valores
        // positivos, e é onde nasce o defeito de estorno: um sistema que
        // arredonda uma cobrança com Truncar e o estorno dela com ParaBaixo
        // devolve um centavo a mais.
        for (var milesimos = 1; milesimos <= 50_000; milesimos++)
        {
            var positivo = milesimos / 1000m;

            Assert.Equal(
                Arredondar.Para(positivo, Arredondamento.Truncar),
                Arredondar.Para(positivo, Arredondamento.ParaBaixo));
        }

        var divergiram = 0;

        for (var milesimos = 1; milesimos <= 50_000; milesimos++)
        {
            var negativo = -milesimos / 1000m;

            if (Arredondar.Para(negativo, Arredondamento.Truncar)
                != Arredondar.Para(negativo, Arredondamento.ParaBaixo))
            {
                divergiram++;
            }
        }

        // Divergem em todo valor negativo que NÃO é inteiro: 50.000 menos os 50
        // inteiros.
        Assert.Equal(50_000 - 50, divergiram);
    }

    [Fact(DisplayName = "a conversão de decimal para centavos exige o modo")]
    public void AConversao()
    {
        // 1,005 em centavos é 100,5 -- um empate exato, e a resposta depende do
        // modo. É por isso que não existe sobrecarga sem modo.
        Assert.Equal(
            Valor.DeCentavos(100),
            Valor.De(1.005m, Arredondamento.ParaOPar));

        Assert.Equal(
            Valor.DeCentavos(101),
            Valor.De(1.005m, Arredondamento.ParaLongeDoZero));

        Assert.Equal(
            Valor.DeCentavos(100),
            Valor.De(1.005m, Arredondamento.Truncar));

        // E a volta é exata, sempre.
        var sorteio = new Random(11);

        for (var caso = 0; caso < 100_000; caso++)
        {
            var valor = Valor.DeCentavos(sorteio.NextInt64(-1_000_000_000, 1_000_000_000));

            Assert.Equal(valor, Valor.De(valor.ComoDecimal(), Arredondamento.ParaOPar));
            Assert.Equal(valor.Centavos, (long)(valor.ComoDecimal() * 100));
        }
    }

    [Fact(DisplayName = "todos os modos têm nome, e a lista está completa")]
    public void OsNomes()
    {
        Assert.Equal(6, Arredondar.Todos.Count);
        Assert.Equal(6, Enum.GetValues<Arredondamento>().Length);

        foreach (var modo in Arredondar.Todos)
        {
            Assert.False(string.IsNullOrWhiteSpace(Arredondar.Nome(modo)));
        }

        Assert.Equal(6, Arredondar.Todos.Select(Arredondar.Nome).Distinct().Count());
    }
}
