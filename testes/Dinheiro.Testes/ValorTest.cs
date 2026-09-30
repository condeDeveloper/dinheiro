using Xunit;

namespace Conde.Dinheiro.Testes;

/// <summary>
/// A quantia — contra o <c>decimal</c>, e contra o <c>double</c> que não serve.
/// </summary>
/// <remarks>
/// O <c>decimal</c> do .NET é o juiz: ele representa valores decimais
/// exatamente, então a soma de um milhão de quantias em <c>decimal</c> é a
/// resposta certa, e a soma em centavos inteiros tem de bater com ela.
/// </remarks>
public class ValorTest
{
    [Fact(DisplayName = "0,1 + 0,2 não dá 0,3 — e é por isso que este projeto existe")]
    public void OMotivo()
    {
        // Não é defeito do C#: é o IEEE 754, e vale igual em Java, Python,
        // JavaScript e C. O double mais próximo de 0,1 é
        // 0,1000000000000000055511151231257827...
        Assert.NotEqual(0.3, 0.1 + 0.2);

        Assert.True(Math.Abs(0.1 + 0.2 - 0.3) > 0);

        // Em decimal, dá.
        Assert.Equal(0.3m, 0.1m + 0.2m);

        // E em centavos inteiros, trivialmente.
        Assert.Equal(
            Valor.De(0, 30),
            Valor.De(0, 10) + Valor.De(0, 20));
    }

    [Fact(DisplayName = "somar um milhão de quantias em double erra; em centavos, não")]
    public void OErroAcumulado()
    {
        var sorteio = new Random(29);

        var quantias = new Valor[1_000_000];

        double emDouble = 0;
        decimal emDecimal = 0;

        for (var i = 0; i < quantias.Length; i++)
        {
            var centavos = sorteio.Next(1, 100_000);

            quantias[i] = Valor.DeCentavos(centavos);

            emDouble += centavos / 100.0;
            emDecimal += centavos / 100m;
        }

        var emCentavos = Valor.Somar(quantias);

        // O decimal é o juiz, e a soma em centavos bate com ele EXATAMENTE.
        Assert.Equal(emDecimal, emCentavos.ComoDecimal());

        // O double, não. A diferença é de centavos num total de milhões -- o
        // suficiente para um fechamento não bater.
        Assert.NotEqual(emDecimal, (decimal)emDouble);

        var diferenca = Math.Abs((decimal)emDouble - emDecimal);

        Assert.True(diferenca > 0,
            $"esperava o double errar e ele acertou em {quantias.Length} somas");
    }

    [Fact(DisplayName = "a aritmética bate com o decimal, em 200 mil operações")]
    public void ContraODecimal()
    {
        var sorteio = new Random(7);

        for (var caso = 0; caso < 200_000; caso++)
        {
            var um = Valor.DeCentavos(sorteio.Next(-10_000_000, 10_000_000));
            var outro = Valor.DeCentavos(sorteio.Next(-10_000_000, 10_000_000));

            Assert.Equal(um.ComoDecimal() + outro.ComoDecimal(), (um + outro).ComoDecimal());
            Assert.Equal(um.ComoDecimal() - outro.ComoDecimal(), (um - outro).ComoDecimal());
            Assert.Equal(-um.ComoDecimal(), (-um).ComoDecimal());

            var quantas = sorteio.Next(-100, 100);

            Assert.Equal(um.ComoDecimal() * quantas, (um * quantas).ComoDecimal());

            // A comparação também.
            Assert.Equal(um.ComoDecimal().CompareTo(outro.ComoDecimal()), um.CompareTo(outro));
            Assert.Equal(um.ComoDecimal() == outro.ComoDecimal(), um == outro);
            Assert.Equal(um.ComoDecimal() < outro.ComoDecimal(), um < outro);
        }
    }

    [Fact(DisplayName = "a taxa: multiplicar por porcentagem bate com o decimal arredondado")]
    public void ATaxa()
    {
        var sorteio = new Random(13);

        for (var caso = 0; caso < 100_000; caso++)
        {
            var valor = Valor.DeCentavos(sorteio.Next(0, 10_000_000));

            var taxa = sorteio.Next(0, 10_000) / 10_000m;

            foreach (var modo in Arredondar.Todos)
            {
                var esperado = Arredondar.Para(valor.Centavos * taxa, modo);

                Assert.Equal(Valor.DeCentavos(esperado), valor.Vezes(taxa, modo));
            }
        }

        // Os casos nomeados: 7,5% de R$ 10,00 é exato; 8,25% não é.
        Assert.Equal(
            Valor.De(0, 75),
            Valor.De(10, 0).Vezes(0.075m, Arredondamento.ParaOPar));

        Assert.Equal(
            Valor.DeCentavos(82),
            Valor.De(10, 0).Vezes(0.0825m, Arredondamento.ParaOPar));

        Assert.Equal(
            Valor.DeCentavos(83),
            Valor.De(10, 0).Vezes(0.0825m, Arredondamento.ParaLongeDoZero));
    }

    [Fact(DisplayName = "reais e centavos, inclusive nos negativos")]
    public void ReaisECentavos()
    {
        Assert.Equal(1_234_56, Valor.De(1_234, 56).Centavos);
        Assert.Equal(1_234, Valor.De(1_234, 56).Reais);
        Assert.Equal(56, Valor.De(1_234, 56).Parte);

        // O sinal vem dos reais: -1 real e 50 centavos é -1,50.
        Assert.Equal(-150, Valor.De(-1, 50).Centavos);
        Assert.Equal(-1, Valor.De(-1, 50).Reais);
        Assert.Equal(50, Valor.De(-1, 50).Parte);

        Assert.True(Valor.De(-1, 50).Negativa);
        Assert.False(Valor.Zero.Negativa);
        Assert.True(Valor.Zero.EhZero);

        Assert.Equal(Valor.De(1, 50), Valor.De(-1, 50).Absoluto());

        Assert.Throws<ArgumentOutOfRangeException>(() => Valor.De(1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => Valor.De(1, -1));

        Assert.Throws<DivideByZeroException>(
            () => Valor.De(10, 0).Dividido(0, Arredondamento.ParaOPar));

        Assert.Throws<ArgumentNullException>(() => Valor.Somar(null!));
    }

    [Fact(DisplayName = "o formato em reais é igual em qualquer máquina")]
    public void OFormato()
    {
        Assert.Equal("R$ 1.234,56", Valor.De(1_234, 56).EmReais());
        Assert.Equal("R$ 0,05", Valor.DeCentavos(5).EmReais());
        Assert.Equal("R$ 0,00", Valor.Zero.EmReais());
        Assert.Equal("-R$ 1,50", Valor.De(-1, 50).EmReais());
        Assert.Equal("R$ 1.000.000,00", Valor.De(1_000_000, 0).EmReais());

        // O ToString é o formato neutro, com ponto decimal -- para registro e
        // para serialização, onde a vírgula estragaria um CSV.
        Assert.Equal("1234.56", Valor.De(1_234, 56).ToString());
        Assert.Equal("-1.50", Valor.De(-1, 50).ToString());
    }

    [Fact(DisplayName = "o estouro é detectado, e não vira silêncio")]
    public void OEstouro()
    {
        // Um long de centavos vai até uns 92 quatrilhões de reais, que é mais
        // que o dinheiro do mundo. Mesmo assim a soma é `checked`: um estouro
        // silencioso viraria um saldo negativo enorme, e isso é pior que uma
        // exceção.
        var quase = Valor.DeCentavos(long.MaxValue);

        Assert.Throws<OverflowException>(() => quase + Valor.DeCentavos(1));
        Assert.Throws<OverflowException>(() => quase * 2);

        var negativoExtremo = Valor.DeCentavos(long.MinValue);

        Assert.Throws<OverflowException>(() => negativoExtremo - Valor.DeCentavos(1));
        Assert.Throws<OverflowException>(() => -negativoExtremo);
    }
}
