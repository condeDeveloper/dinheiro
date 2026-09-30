using System.Diagnostics;
using Conde.Dinheiro;

namespace Conde.Dinheiro.Ferramentas.Medidor;

/// <summary>
/// A ferramenta — que mostra o erro acontecendo e registra os números.
/// </summary>
/// <remarks>
/// Tudo que depende de relógio mora aqui, e não na suíte de testes. Uma
/// afirmação de tempo falha sem que nada tenha piorado — basta a máquina do CI
/// estar ocupada — e o efeito é ensinar quem trabalha no projeto a ignorar teste
/// vermelho. Esta ferramenta imprime e sai com zero, sempre.
/// </remarks>
public static class Programa
{
    public static int Main(string[] argumentos)
    {
        // Procura o comando entre TODOS os argumentos. O `dotnet run` nem sempre
        // entrega só o que vem depois do `--`, e um `argumentos[0]` cru já
        // derrubou o CI de outro projeto daqui com o código todo funcionando.
        var comandos = new[] { "double", "rateio", "vies", "modos", "velocidade" };

        var onde = Array.FindIndex(argumentos, a => comandos.Contains(a));

        if (onde < 0)
        {
            Console.WriteLine(Ajuda);

            return 0;
        }

        switch (argumentos[onde])
        {
            case "double":
                ODouble();
                break;

            case "rateio":
                ORateio();
                break;

            case "vies":
                OVies();
                break;

            case "modos":
                OsModos();
                break;

            default:
                Velocidade();
                break;
        }

        return 0;
    }

    /// <summary>O erro do ponto flutuante, acontecendo.</summary>
    private static void ODouble()
    {
        Console.WriteLine("Por que dinheiro não mora num double");
        Console.WriteLine();
        Console.WriteLine($"  0.1 + 0.2 == 0.3   →   {0.1 + 0.2 == 0.3}");
        Console.WriteLine($"  0.1 + 0.2          =    {0.1 + 0.2:R}");
        Console.WriteLine($"  o double de 0.1    =    {0.1:G17}");
        Console.WriteLine();
        Console.WriteLine("  Não é defeito do C#: é o IEEE 754, e vale igual em Java, Python,");
        Console.WriteLine("  JavaScript e C. 0,1 não existe em binário, do mesmo jeito que 1/3 não");
        Console.WriteLine("  existe em decimal.");
        Console.WriteLine();

        Console.WriteLine("  Somando quantias sorteadas:");
        Console.WriteLine();
        Console.WriteLine("       quantas        em double          em centavos         erro");
        Console.WriteLine("  " + new string('-', 70));

        var sorteio = new Random(29);

        var quantias = new List<Valor>();

        double emDouble = 0;

        var proximoMarco = 1_000;

        for (var i = 1; i <= 1_000_000; i++)
        {
            var centavos = sorteio.Next(1, 100_000);

            quantias.Add(Valor.DeCentavos(centavos));

            emDouble += centavos / 100.0;

            if (i == proximoMarco)
            {
                var exato = Valor.Somar(quantias);

                var erro = (decimal)emDouble - exato.ComoDecimal();

                Console.WriteLine(
                    $"  {i,12:N0} {emDouble,17:F2} {exato.ComoDecimal(),18:F2}"
                    + $"   {erro,12:F6}");

                proximoMarco *= 10;
            }
        }

        Console.WriteLine();
        Console.WriteLine("  O erro CRESCE com o número de somas -- ele não se cancela. E vale ser");
        Console.WriteLine("  honesto sobre a escala: com um milhão de valores sorteados ele ainda");
        Console.WriteLine("  não chegou a um centavo, está na casa de 0,00001.");
        Console.WriteLine();
        Console.WriteLine("  Por que isso já é motivo suficiente para não usar double:");
        Console.WriteLine();
        Console.WriteLine("    1. o erro depende da ORDEM das parcelas, então somar os mesmos");
        Console.WriteLine("       lançamentos em ordem diferente dá totais diferentes -- e dois");
        Console.WriteLine("       relatórios do mesmo dia deixam de bater entre si;");
        Console.WriteLine();
        Console.WriteLine("    2. a comparação quebra ANTES da soma: `0.1 + 0.2 == 0.3` é falso, e");
        Console.WriteLine("       um `if (saldo == 0)` pode não disparar num saldo que é zero;");
        Console.WriteLine();
        Console.WriteLine("    3. com valores grandes o erro cresce junto -- o double tem 15 a 16");
        Console.WriteLine("       algarismos, e um total na casa dos bilhões já não tem centavo");
        Console.WriteLine("       confiável.");
        Console.WriteLine();
        Console.WriteLine("  Em centavos inteiros nada disso existe: a soma é exata em qualquer");
        Console.WriteLine("  ordem, em qualquer escala, e a comparação é comparação de inteiros.");
    }

    /// <summary>O problema de repartir R$ 100,00 entre três.</summary>
    private static void ORateio()
    {
        Console.WriteLine("Dividir R$ 100,00 entre três pessoas");
        Console.WriteLine();

        var cem = Valor.De(100, 0);

        var ingenua = cem.Dividido(3, Arredondamento.ParaOPar);
        var paraCima = cem.Dividido(3, Arredondamento.ParaCima);

        Console.WriteLine("  resposta                 partes                        soma      fecha?");
        Console.WriteLine("  " + new string('-', 76));

        Console.WriteLine($"  33,33 para cada      {ingenua.EmReais()}  {ingenua.EmReais()}  {ingenua.EmReais()}"
            + $"   {(ingenua * 3).EmReais(),12}   {((ingenua * 3) == cem ? "sim" : "NÃO, falta 1 centavo")}");

        Console.WriteLine($"  33,34 para cada      {paraCima.EmReais()}  {paraCima.EmReais()}  {paraCima.EmReais()}"
            + $"   {(paraCima * 3).EmReais(),12}   {((paraCima * 3) == cem ? "sim" : "NÃO, sobram 2 centavos")}");

        var certo = Rateio.EmPartesIguais(cem, 3);

        Console.WriteLine($"  o rateio             {certo[0].EmReais()}  {certo[1].EmReais()}  {certo[2].EmReais()}"
            + $"   {Valor.Somar(certo).EmReais(),12}   sim");

        Console.WriteLine();
        Console.WriteLine("  A invariante não é uma aproximação: a soma das partes É o todo, sempre.");
        Console.WriteLine("  Repartir uma quantia e não obter a quantia de volta não é um");
        Console.WriteLine("  arredondamento ruim -- é outra operação.");
        Console.WriteLine();

        Console.WriteLine("  E o rateio por pesos, que é o de frete e de desconto:");
        Console.WriteLine();
        Console.WriteLine("    um desconto de R$ 0,01 entre dois itens de 70% e 30%:");

        var desconto = Rateio.PorPesos(Valor.DeCentavos(1), [700, 300]);

        Console.WriteLine($"      {desconto[0].EmReais()} e {desconto[1].EmReais()}"
            + $"  =  {Valor.Somar(desconto).EmReais()}");
        Console.WriteLine();
        Console.WriteLine("    R$ 1.000,00 de frete entre pedidos de 3, 5 e 7 kg:");

        var frete = Rateio.PorPesos(Valor.De(1_000, 0), [3, 5, 7]);

        Console.WriteLine($"      {frete[0].EmReais()}, {frete[1].EmReais()} e {frete[2].EmReais()}"
            + $"  =  {Valor.Somar(frete).EmReais()}");
        Console.WriteLine();
        Console.WriteLine("  Quem perde mais na divisão recebe o centavo que sobra -- é o método do");
        Console.WriteLine("  maior resto, o mesmo de Hamilton na repartição de cadeiras.");
    }

    /// <summary>O viés de cada modo de arredondamento.</summary>
    private static void OVies()
    {
        Console.WriteLine("O viés de cada modo — cem mil empates exatos, de 0,5 a 99.999,5");
        Console.WriteLine();
        Console.WriteLine("  modo             soma arredondada      soma exata          erro");
        Console.WriteLine("  " + new string('-', 68));

        decimal exata = 0;

        var somas = new Dictionary<Arredondamento, long>();

        foreach (var modo in Arredondar.Todos)
        {
            somas[modo] = 0;
        }

        for (var i = 0; i < 100_000; i++)
        {
            var valor = i + 0.5m;

            exata += valor;

            foreach (var modo in Arredondar.Todos)
            {
                somas[modo] += Arredondar.Para(valor, modo);
            }
        }

        foreach (var modo in Arredondar.Todos)
        {
            var erro = somas[modo] - exata;

            Console.WriteLine(
                $"  {Arredondar.Nome(modo),-16} {somas[modo],17:N0} {exata,17:N0}"
                + $"   {erro,11:N0}" + (erro == 0 ? "   ← sem viés" : string.Empty));
        }

        Console.WriteLine();
        Console.WriteLine("  \"Para o par\" é o único que erra ZERO -- não \"quase zero\": zero.");
        Console.WriteLine("  Metade dos empates sobe e metade desce, e eles se cancelam exatamente.");
        Console.WriteLine();
        Console.WriteLine("  \"Longe do zero\", que é o que se aprende na escola, erra meio por");
        Console.WriteLine("  empate -- e num milhão de linhas isso são R$ 5.000,00.");
        Console.WriteLine();
        Console.WriteLine("  É por isso que a norma contábil de quase todo lugar manda usar o par.");
        Console.WriteLine("  E é por isso que a legislação tributária brasileira, que manda o");
        Console.WriteLine("  contrário, é uma decisão política e não técnica: a lei ganha da");
        Console.WriteLine("  estatística, e o sistema tem de obedecer.");
    }

    /// <summary>Onde os modos divergem, e onde não.</summary>
    private static void OsModos()
    {
        Console.WriteLine("Onde os seis modos divergem");
        Console.WriteLine();

        Console.Write("     valor  ");

        foreach (var modo in Arredondar.Todos)
        {
            Console.Write($"{Arredondar.Nome(modo),15}");
        }

        Console.WriteLine();
        Console.WriteLine("  " + new string('-', 100));

        foreach (var quartos in (int[])[-10, -6, -5, -4, -2, -1, 0, 1, 2, 4, 5, 6, 10])
        {
            var valor = quartos * 0.5m;

            Console.Write($"  {valor,8:F1}  ");

            foreach (var modo in Arredondar.Todos)
            {
                Console.Write($"{Arredondar.Para(valor, modo),15}");
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("  As linhas de meio exato são as únicas em que os seis discordam. Fora");
        Console.WriteLine("  delas, os quatro modos de vizinho dão o mesmo -- e é por isso que a");
        Console.WriteLine("  escolha parece não importar até somar um milhão de vezes.");
        Console.WriteLine();
        Console.WriteLine("  Repare na coluna \"truncar\" contra \"para baixo\": elas só divergem nos");
        Console.WriteLine("  NEGATIVOS. Um teste que só usa valores positivos não vê a diferença, e");
        Console.WriteLine("  é aí que nasce o defeito de estorno -- cobrar com um modo e devolver");
        Console.WriteLine("  com o outro devolve um centavo a mais.");
    }

    /// <summary>A velocidade das três representações.</summary>
    private static void Velocidade()
    {
        Console.WriteLine("A velocidade de somar dez milhões de quantias");
        Console.WriteLine();

        const int quantas = 10_000_000;

        var sorteio = new Random(29);

        var centavos = new long[quantas];

        for (var i = 0; i < quantas; i++)
        {
            centavos[i] = sorteio.Next(1, 100_000);
        }

        var relogio = Stopwatch.StartNew();

        long emLong = 0;

        foreach (var c in centavos)
        {
            emLong += c;
        }

        var tempoLong = relogio.Elapsed.TotalMilliseconds;

        relogio.Restart();

        decimal emDecimal = 0;

        foreach (var c in centavos)
        {
            emDecimal += c / 100m;
        }

        var tempoDecimal = relogio.Elapsed.TotalMilliseconds;

        relogio.Restart();

        double emDouble = 0;

        foreach (var c in centavos)
        {
            emDouble += c / 100.0;
        }

        var tempoDouble = relogio.Elapsed.TotalMilliseconds;

        Console.WriteLine("  representação       tempo        razão    exata?");
        Console.WriteLine("  " + new string('-', 52));
        Console.WriteLine($"  long de centavos  {tempoLong,7:F1} ms      1.0x    sim");
        Console.WriteLine($"  decimal           {tempoDecimal,7:F1} ms   {tempoDecimal / tempoLong,6:F1}x    sim");
        Console.WriteLine($"  double            {tempoDouble,7:F1} ms   {tempoDouble / tempoLong,6:F1}x    NÃO");

        Console.WriteLine();
        Console.WriteLine($"  (a soma em long deu {Valor.DeCentavos(emLong).EmReais()};");
        Console.WriteLine($"   em decimal, {emDecimal:F2}; em double, {emDouble:F2})");
        Console.WriteLine();
        Console.WriteLine("  O decimal é exato e serve -- é o que a maioria usa, e é o juiz desta");
        Console.WriteLine("  suíte de testes. O que ele custa é tempo, e o que ele arrisca é que");
        Console.WriteLine("  dividir dois decimal devolve um decimal com casas que ninguém pediu.");
        Console.WriteLine();
        Console.WriteLine("  O double é o mais rápido dos três e é o único errado. A velocidade");
        Console.WriteLine("  dele não compra nada: um total que não fecha não vale mais rápido.");
    }

    private const string Ajuda = """
        dinheiro -- quantias em centavos inteiros, e o rateio que fecha

          medidor double       o erro do ponto flutuante, acontecendo
          medidor rateio       dividir R$ 100,00 entre três
          medidor vies         o viés de cada modo de arredondamento
          medidor modos        onde os seis modos divergem
          medidor velocidade   long, decimal e double
        """;
}
