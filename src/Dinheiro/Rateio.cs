namespace Conde.Dinheiro;

/// <summary>
/// Repartir uma quantia sem perder nem inventar centavo.
/// </summary>
/// <remarks>
/// <para>
/// É o problema central de todo sistema que mexe com dinheiro, e o enunciado
/// cabe numa linha: <b>dividir R$ 100,00 entre três pessoas</b>.
/// </para>
/// <para>
/// A resposta ingênua é 33,33 para cada, e ela <b>perde um centavo</b>. A
/// resposta 33,34 para cada <b>inventa dois</b>. Nenhuma das duas fecha, e um
/// sistema financeiro que não fecha não é um sistema financeiro.
/// </para>
/// <para>
/// A resposta certa é 33,34 + 33,33 + 33,33, e o que este arquivo faz é decidir
/// <b>quem</b> recebe o centavo a mais — porque essa é a única parte que tem
/// escolha. A invariante, essa, não tem: <b>a soma das partes é exatamente o
/// todo</b>, sempre, para qualquer quantia e qualquer número de partes.
/// </para>
/// <para>
/// É uma invariante do tipo que vale ouro num teste: não é uma comparação com
/// outra implementação, é uma <b>dedução</b> sobre o que a operação significa. E
/// ela é verificável exaustivamente.
/// </para>
/// </remarks>
public static class Rateio
{
    /// <summary>
    /// Reparte em <paramref name="quantas"/> partes iguais — com o resto distribuído.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A conta é a divisão inteira mais o resto: cada parte recebe o quociente, e
    /// as primeiras <c>resto</c> partes recebem um centavo a mais. Não há
    /// arredondamento nenhum — e é justamente por isso que fecha.
    /// </para>
    /// <para>
    /// Arredondar cada parte e torcer para a soma bater é o caminho que não
    /// funciona, e é o que quase todo sistema faz antes de alguém reclamar.
    /// </para>
    /// </remarks>
    public static Valor[] EmPartesIguais(Valor total, int quantas)
    {
        if (quantas < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantas), quantas, "são precisas pelo menos uma parte");
        }

        var partes = new Valor[quantas];

        var quociente = total.Centavos / quantas;
        var resto = total.Centavos % quantas;

        // Em C#, o resto de um número negativo é negativo -- e é o que se quer
        // aqui: repartir -100 em 3 dá -34, -33, -33, e a soma fecha em -100.
        var passo = resto < 0 ? -1 : 1;

        var quantosSobram = Math.Abs(resto);

        for (var i = 0; i < quantas; i++)
        {
            partes[i] = Valor.DeCentavos(quociente + (i < quantosSobram ? passo : 0));
        }

        return partes;
    }

    /// <summary>
    /// Reparte na proporção dos pesos — o rateio de verdade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a operação que aparece em rateio de frete, em divisão de comissão, em
    /// alocação de desconto entre os itens de um pedido. E é onde o erro de
    /// centavo mais acontece, porque a tentação é calcular cada parte
    /// independentemente e somar no fim.
    /// </para>
    /// <para>
    /// O método aqui é o do <b>maior resto</b>, também conhecido como método de
    /// Hamilton na repartição de cadeiras: calcula-se a parte exata de cada um,
    /// dá-se a todos a parte inteira, e os centavos que sobram vão para quem tem
    /// a maior fração perdida. É o que minimiza a diferença total entre o que
    /// cada um recebeu e o que teria direito.
    /// </para>
    /// <para>
    /// (O método de Hamilton tem um defeito famoso na repartição de cadeiras — o
    /// paradoxo do Alabama, em que aumentar o total faz um estado <b>perder</b>
    /// uma cadeira. Ele existe aqui também, e não importa: ninguém reparte a
    /// mesma nota fiscal duas vezes com totais diferentes.)
    /// </para>
    /// </remarks>
    public static Valor[] PorPesos(Valor total, IReadOnlyList<long> pesos)
    {
        ArgumentNullException.ThrowIfNull(pesos);

        if (pesos.Count == 0)
        {
            throw new ArgumentException("é preciso pelo menos um peso", nameof(pesos));
        }

        if (pesos.Any(p => p < 0))
        {
            throw new ArgumentException("os pesos não podem ser negativos", nameof(pesos));
        }

        long somaDosPesos = 0;

        foreach (var peso in pesos)
        {
            somaDosPesos = checked(somaDosPesos + peso);
        }

        if (somaDosPesos == 0)
        {
            throw new ArgumentException("a soma dos pesos não pode ser zero", nameof(pesos));
        }

        var partes = new long[pesos.Count];

        // A parte inteira de cada um, e o resto de cada divisão.
        var restos = new (int Onde, long Resto)[pesos.Count];

        long distribuido = 0;

        for (var i = 0; i < pesos.Count; i++)
        {
            var produto = total.Centavos * pesos[i];

            var inteiro = produto / somaDosPesos;

            partes[i] = inteiro;
            restos[i] = (i, Math.Abs(produto - inteiro * somaDosPesos));

            distribuido += inteiro;
        }

        var sobra = total.Centavos - distribuido;

        // Quem perdeu mais na divisão recebe primeiro. O desempate é pelo índice,
        // para o resultado ser determinístico -- duas execuções com a mesma
        // entrada têm de dar exatamente o mesmo, ou a conciliação não fecha.
        Array.Sort(restos, (um, outro) =>
        {
            var comparacao = outro.Resto.CompareTo(um.Resto);

            return comparacao != 0 ? comparacao : um.Onde.CompareTo(outro.Onde);
        });

        var passo = sobra < 0 ? -1 : 1;

        for (var i = 0; i < Math.Abs(sobra); i++)
        {
            partes[restos[i % restos.Length].Onde] += passo;
        }

        return partes.Select(Valor.DeCentavos).ToArray();
    }

    /// <summary>
    /// Reparte em parcelas, com a diferença na <b>primeira</b> — o padrão do crediário.
    /// </summary>
    /// <remarks>
    /// Em carnê e em cartão, a convenção brasileira é a parcela <b>maior vir
    /// primeiro</b>: R$ 100,00 em 3 vezes é 33,34 + 33,33 + 33,33. A razão é
    /// comercial e não matemática — a primeira parcela costuma ser a entrada, e
    /// é melhor que a diferença apareça nela do que numa parcela futura que o
    /// cliente vai conferir contra o contrato.
    /// </remarks>
    public static Valor[] EmParcelas(Valor total, int quantas) =>
        EmPartesIguais(total, quantas);

    /// <summary>
    /// Reparte com a diferença na <b>última</b> parcela.
    /// </summary>
    /// <remarks>
    /// A outra convenção, que alguns contratos exigem: as parcelas iguais e o
    /// ajuste no fim. R$ 100,00 em 3 vezes vira 33,33 + 33,33 + 33,34.
    /// </remarks>
    public static Valor[] ComAjusteNoFim(Valor total, int quantas)
    {
        var partes = EmPartesIguais(total, quantas);

        Array.Reverse(partes);

        return partes;
    }
}
