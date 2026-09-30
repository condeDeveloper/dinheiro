using System.Globalization;

namespace Conde.Dinheiro;

/// <summary>
/// Uma quantia — guardada em <b>centavos inteiros</b>, e nunca em ponto flutuante.
/// </summary>
/// <remarks>
/// <para>
/// A regra é uma só e não tem exceção: <b>dinheiro não mora num <c>double</c></b>.
/// O motivo é que 0,1 não existe em binário, do mesmo jeito que 1/3 não existe em
/// decimal. O <c>double</c> mais próximo de 0,1 é
/// 0,1000000000000000055511151231257827…, e a diferença aparece na terceira soma:
/// </para>
/// <code>
///   0.1 + 0.2 == 0.3   →   false
/// </code>
/// <para>
/// Não é um defeito do C#. É o IEEE 754, e vale igual em Java, Python, JavaScript
/// e C. Um sistema que somasse um milhão de lançamentos em <c>double</c> erraria
/// centavos, e um sistema financeiro que erra centavos não fecha o dia.
/// </para>
/// <para>
/// Aqui a quantia é um <c>long</c> de centavos. Somar é somar inteiros, e
/// inteiro não tem erro de representação nenhum — a soma de um milhão de valores
/// é <b>exata</b>. O que sobra de difícil é a <b>divisão</b>, que é onde este
/// projeto passa a maior parte do tempo.
/// </para>
/// <para>
/// (O <c>decimal</c> do .NET também serve, e é o que a maioria usa. Ele guarda
/// 96 bits de mantissa com expoente decimal, então representa 0,1 exatamente. É
/// mais caro que um <c>long</c> e mais fácil de usar errado — dividir dois
/// <c>decimal</c> devolve um <c>decimal</c> com casas que ninguém pediu. Aqui ele
/// aparece como <b>juiz</b>, e não como armazenamento.)
/// </para>
/// </remarks>
public readonly struct Valor : IEquatable<Valor>, IComparable<Valor>
{
    /// <summary>A quantia em centavos — o único campo.</summary>
    public long Centavos { get; }

    private Valor(long centavos) => Centavos = centavos;

    /// <summary>Zero.</summary>
    public static Valor Zero => new(0);

    /// <summary>Uma quantia a partir de centavos.</summary>
    public static Valor DeCentavos(long centavos) => new(centavos);

    /// <summary>Uma quantia a partir de reais e centavos.</summary>
    public static Valor De(long reais, int centavos)
    {
        if (centavos is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(centavos), centavos, "os centavos vão de 0 a 99");
        }

        // O sinal vem dos reais: -1 real e 50 centavos é -1,50, e não -0,50.
        return new Valor(reais < 0 ? reais * 100 - centavos : reais * 100 + centavos);
    }

    /// <summary>
    /// Uma quantia a partir de um <c>decimal</c> — arredondando, de propósito.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A conversão <b>arredonda</b>, e o modo é obrigatório na assinatura. Não há
    /// versão sem modo, e isso é deliberado: converter 1,005 em centavos é uma
    /// decisão de negócio, e quem chama tem de tomá-la por escrito.
    /// </para>
    /// <para>
    /// Uma sobrecarga sem parâmetro escolheria em silêncio — e um arredondamento
    /// escolhido em silêncio, repetido um milhão de vezes, é a diferença entre
    /// fechar e não fechar o caixa.
    /// </para>
    /// </remarks>
    public static Valor De(decimal reais, Arredondamento modo) =>
        new(Arredondar.Para(reais * 100, modo));

    /// <summary>A quantia como <c>decimal</c>, exata.</summary>
    /// <remarks>
    /// A conversão para <c>decimal</c> é <b>exata</b> e não perde nada: todo
    /// <c>long</c> de centavos cabe na mantissa de 96 bits. É a conversão para
    /// <c>double</c> que perderia, e ela não existe nesta classe de propósito.
    /// </remarks>
    public decimal ComoDecimal() => Centavos / 100m;

    /// <summary>A parte inteira, em reais — truncada para o zero.</summary>
    public long Reais => Centavos / 100;

    /// <summary>A parte de centavos, sempre de 0 a 99.</summary>
    public int Parte => (int)Math.Abs(Centavos % 100);

    /// <summary>É negativa?</summary>
    public bool Negativa => Centavos < 0;

    /// <summary>É zero?</summary>
    public bool EhZero => Centavos == 0;

    /// <summary>O valor absoluto.</summary>
    public Valor Absoluto() => new(Math.Abs(Centavos));

    /// <summary>A soma — exata, sempre.</summary>
    public static Valor operator +(Valor um, Valor outro) =>
        new(checked(um.Centavos + outro.Centavos));

    /// <summary>A diferença — exata, sempre.</summary>
    public static Valor operator -(Valor um, Valor outro) =>
        new(checked(um.Centavos - outro.Centavos));

    /// <summary>O oposto.</summary>
    public static Valor operator -(Valor valor) => new(checked(-valor.Centavos));

    /// <summary>Multiplicação por um número inteiro — exata.</summary>
    public static Valor operator *(Valor valor, long quantas) =>
        new(checked(valor.Centavos * quantas));

    /// <summary>
    /// Multiplicação por uma taxa — que <b>exige</b> um modo de arredondamento.
    /// </summary>
    /// <remarks>
    /// Não há operador <c>*</c> para <c>decimal</c>, e a ausência é o ponto.
    /// Multiplicar R$ 10,00 por 7,5% dá R$ 0,75 exatos; por 8,25% dá R$ 0,825, e
    /// aí alguém tem de decidir. Um operador esconderia a decisão dentro de um
    /// símbolo.
    /// </remarks>
    public Valor Vezes(decimal taxa, Arredondamento modo) =>
        new(Arredondar.Para(Centavos * taxa, modo));

    /// <summary>Divisão por um inteiro, com modo — para uma parcela só.</summary>
    /// <remarks>
    /// Para repartir uma quantia <b>inteira</b> entre vários, esta não é a
    /// operação certa: dividir 100 por 3 e multiplicar de volta dá 99,99. O que
    /// se quer é o <see cref="Rateio"/>, que reparte sem perder centavo.
    /// </remarks>
    public Valor Dividido(long quantas, Arredondamento modo)
    {
        if (quantas == 0)
        {
            throw new DivideByZeroException("não dá para dividir uma quantia por zero");
        }

        return new Valor(Arredondar.Para((decimal)Centavos / quantas, modo));
    }

    public bool Equals(Valor outra) => Centavos == outra.Centavos;

    public override bool Equals(object? outro) => outro is Valor valor && Equals(valor);

    public override int GetHashCode() => Centavos.GetHashCode();

    public int CompareTo(Valor outra) => Centavos.CompareTo(outra.Centavos);

    public static bool operator ==(Valor um, Valor outro) => um.Centavos == outro.Centavos;

    public static bool operator !=(Valor um, Valor outro) => um.Centavos != outro.Centavos;

    public static bool operator <(Valor um, Valor outro) => um.Centavos < outro.Centavos;

    public static bool operator >(Valor um, Valor outro) => um.Centavos > outro.Centavos;

    public static bool operator <=(Valor um, Valor outro) => um.Centavos <= outro.Centavos;

    public static bool operator >=(Valor um, Valor outro) => um.Centavos >= outro.Centavos;

    /// <summary>A soma de uma sequência — exata, sem acumular erro nenhum.</summary>
    public static Valor Somar(IEnumerable<Valor> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        long total = 0;

        foreach (var valor in valores)
        {
            total = checked(total + valor.Centavos);
        }

        return new Valor(total);
    }

    /// <summary>No formato brasileiro: <c>R$ 1.234,56</c>.</summary>
    /// <remarks>
    /// O formato é montado à mão, e não pela cultura <c>pt-BR</c>, porque o
    /// projeto roda com <c>InvariantGlobalization</c> — sem as tabelas do ICU, e
    /// portanto igual em qualquer máquina e em qualquer sistema. Um formato de
    /// dinheiro que muda conforme a máquina é um relatório que não bate com o
    /// outro.
    /// </remarks>
    public string EmReais()
    {
        var sinal = Negativa ? "-" : string.Empty;

        var reais = Math.Abs(Reais).ToString("N0", CultureInfo.InvariantCulture)
            .Replace(",", ".", StringComparison.Ordinal);

        return $"{sinal}R$ {reais},{Parte:00}";
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{ComoDecimal():F2}");
}
