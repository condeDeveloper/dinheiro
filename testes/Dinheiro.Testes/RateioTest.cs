using Xunit;

namespace Conde.Dinheiro.Testes;

/// <summary>
/// A invariante do rateio: <b>a soma das partes é exatamente o todo</b>.
/// </summary>
/// <remarks>
/// <para>
/// É o melhor tipo de juiz que existe, e não é uma comparação com implementação
/// nenhuma: é uma <b>dedução sobre o que a operação significa</b>. Repartir uma
/// quantia e não obter a quantia de volta não é uma aproximação ruim — é outra
/// operação.
/// </para>
/// <para>
/// E ela é verificável <b>exaustivamente</b>: todas as quantias de −1.000 a
/// +1.000 centavos, repartidas em todos os números de partes de 1 a 50. São
/// 100.050 rateios, e em nenhum pode faltar ou sobrar um centavo.
/// </para>
/// </remarks>
public class RateioTest
{
    [Fact(DisplayName = "TODOS os 100.050 rateios em partes iguais fecham exatamente")]
    public void AInvarianteExaustiva()
    {
        var quantos = 0;

        for (var centavos = -1_000L; centavos <= 1_000; centavos++)
        {
            var total = Valor.DeCentavos(centavos);

            for (var quantas = 1; quantas <= 50; quantas++)
            {
                var partes = Rateio.EmPartesIguais(total, quantas);

                Assert.Equal(quantas, partes.Length);

                // A invariante.
                Assert.True(total == Valor.Somar(partes),
                    $"repartir {total} em {quantas} deu {Valor.Somar(partes)}");

                // E a segunda, que é quase tão importante: nenhuma parte difere
                // de outra por mais de UM centavo. Repartir "igualmente" e dar a
                // um o dobro do outro fecharia a conta e estaria errado.
                var maior = partes.Max(p => p.Centavos);
                var menor = partes.Min(p => p.Centavos);

                Assert.True(maior - menor <= 1,
                    $"repartir {total} em {quantas}: a maior parte é {maior} e a "
                    + $"menor é {menor}");

                quantos++;
            }
        }

        Assert.Equal(2_001 * 50, quantos);
        Assert.Equal(100_050, quantos);
    }

    [Fact(DisplayName = "o caso que dá nome ao problema: R$ 100,00 entre três")]
    public void OCasoDosCemReais()
    {
        var partes = Rateio.EmPartesIguais(Valor.De(100, 0), 3);

        // 33,34 + 33,33 + 33,33 -- e a soma é exatamente 100,00.
        Assert.Equal(Valor.De(33, 34), partes[0]);
        Assert.Equal(Valor.De(33, 33), partes[1]);
        Assert.Equal(Valor.De(33, 33), partes[2]);

        Assert.Equal(Valor.De(100, 0), Valor.Somar(partes));

        // A resposta ingênua PERDE um centavo.
        var ingenua = Valor.De(100, 0).Dividido(3, Arredondamento.ParaOPar);

        Assert.Equal(Valor.De(33, 33), ingenua);
        Assert.Equal(Valor.De(99, 99), ingenua * 3);
        Assert.NotEqual(Valor.De(100, 0), ingenua * 3);

        // E arredondar para cima INVENTA dois.
        var paraCima = Valor.De(100, 0).Dividido(3, Arredondamento.ParaCima);

        Assert.Equal(Valor.De(33, 34), paraCima);
        Assert.Equal(Valor.De(100, 2), paraCima * 3);

        // A convenção do fim, para quem precisa dela.
        var noFim = Rateio.ComAjusteNoFim(Valor.De(100, 0), 3);

        Assert.Equal(Valor.De(33, 33), noFim[0]);
        Assert.Equal(Valor.De(33, 34), noFim[2]);
        Assert.Equal(Valor.De(100, 0), Valor.Somar(noFim));
    }

    [Fact(DisplayName = "o rateio por pesos também fecha, em 200 mil casos sorteados")]
    public void PorPesos()
    {
        var sorteio = new Random(29);

        for (var caso = 0; caso < 200_000; caso++)
        {
            var total = Valor.DeCentavos(sorteio.Next(-100_000, 100_000));

            var quantos = sorteio.Next(1, 12);

            var pesos = new long[quantos];

            for (var i = 0; i < quantos; i++)
            {
                pesos[i] = sorteio.Next(0, 1_000);
            }

            // Pesos todos zero não são rateio nenhum.
            if (pesos.Sum() == 0)
            {
                pesos[0] = 1;
            }

            var partes = Rateio.PorPesos(total, pesos);

            Assert.True(total == Valor.Somar(partes),
                $"ratear {total} por [{string.Join(", ", pesos)}] deu "
                + $"{Valor.Somar(partes)}");

            // Quem tem peso zero não recebe nada -- e isto é uma consequência da
            // conta, não um caso especial no código.
            for (var i = 0; i < quantos; i++)
            {
                if (pesos[i] == 0)
                {
                    Assert.True(partes[i].EhZero,
                        $"peso zero recebeu {partes[i]}");
                }
            }
        }
    }

    [Fact(DisplayName = "o maior resto: quem perde mais na divisão recebe o centavo")]
    public void OMaiorResto()
    {
        // R$ 10,00 rateados por pesos 1, 1 e 1 caem no caso de partes iguais.
        Assert.Equal(
            [Valor.De(3, 34), Valor.De(3, 33), Valor.De(3, 33)],
            Rateio.PorPesos(Valor.De(10, 0), [1, 1, 1]));

        // Com pesos diferentes, o centavo vai para quem perdeu mais. R$ 1,00 por
        // pesos 1, 1 e 4: as partes exatas são 16,666, 16,666 e 66,666 centavos.
        // Todos perdem 0,666, e o desempate é pelo índice.
        var partes = Rateio.PorPesos(Valor.De(1, 0), [1, 1, 4]);

        Assert.Equal(Valor.De(1, 0), Valor.Somar(partes));
        Assert.Equal(Valor.DeCentavos(17), partes[0]);
        Assert.Equal(Valor.DeCentavos(17), partes[1]);
        Assert.Equal(Valor.DeCentavos(66), partes[2]);

        // E o caso que motiva o método: um desconto de R$ 0,01 rateado entre dois
        // itens. Um recebe o centavo e o outro não, e é impossível que seja de
        // outro jeito.
        var desconto = Rateio.PorPesos(Valor.DeCentavos(1), [700, 300]);

        Assert.Equal(Valor.DeCentavos(1), desconto[0]);
        Assert.Equal(Valor.Zero, desconto[1]);
        Assert.Equal(Valor.DeCentavos(1), Valor.Somar(desconto));
    }

    [Fact(DisplayName = "é determinístico: a mesma entrada dá exatamente a mesma saída")]
    public void Deterministico()
    {
        // Parece óbvio e não é: o desempate por índice existe justamente para
        // isto. Sem ele, a ordenação dos restos empatados dependeria do algoritmo
        // de ordenação, e duas execuções poderiam dar carteiras diferentes -- o
        // que faz a conciliação de um dia não bater com a do outro.
        var sorteio = new Random(7);

        for (var caso = 0; caso < 5_000; caso++)
        {
            var total = Valor.DeCentavos(sorteio.Next(-50_000, 50_000));

            var pesos = Enumerable.Range(0, sorteio.Next(2, 9))
                .Select(_ => (long)sorteio.Next(1, 100))
                .ToArray();

            var primeira = Rateio.PorPesos(total, pesos);

            for (var vez = 0; vez < 3; vez++)
            {
                Assert.Equal(primeira, Rateio.PorPesos(total, pesos));
            }
        }
    }

    [Fact(DisplayName = "as bordas do rateio")]
    public void AsBordas()
    {
        // Uma parte só devolve o todo.
        Assert.Equal([Valor.De(7, 77)], Rateio.EmPartesIguais(Valor.De(7, 77), 1));

        // Zero repartido dá zeros.
        var zeros = Rateio.EmPartesIguais(Valor.Zero, 5);

        Assert.All(zeros, p => Assert.True(p.EhZero));
        Assert.Equal(Valor.Zero, Valor.Somar(zeros));

        // Mais partes que centavos: os primeiros recebem um centavo e o resto
        // recebe zero -- e a soma fecha.
        var espalhado = Rateio.EmPartesIguais(Valor.DeCentavos(3), 10);

        Assert.Equal(Valor.DeCentavos(3), Valor.Somar(espalhado));
        Assert.Equal(3, espalhado.Count(p => p.Centavos == 1));
        Assert.Equal(7, espalhado.Count(p => p.EhZero));

        // Negativos: a diferença vai para o lado certo, e a soma fecha.
        var negativo = Rateio.EmPartesIguais(Valor.De(-100, 0), 3);

        Assert.Equal(Valor.De(-100, 0), Valor.Somar(negativo));
        Assert.Equal(Valor.DeCentavos(-3334), negativo[0]);
        Assert.Equal(Valor.DeCentavos(-3333), negativo[1]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Rateio.EmPartesIguais(Valor.De(10, 0), 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Rateio.EmPartesIguais(Valor.De(10, 0), -1));

        Assert.Throws<ArgumentException>(() => Rateio.PorPesos(Valor.De(10, 0), []));

        Assert.Throws<ArgumentException>(() => Rateio.PorPesos(Valor.De(10, 0), [0, 0]));

        Assert.Throws<ArgumentException>(() => Rateio.PorPesos(Valor.De(10, 0), [1, -1]));

        Assert.Throws<ArgumentNullException>(() => Rateio.PorPesos(Valor.De(10, 0), null!));
    }
}
