namespace RevoltaLetrinhas.Api.Servicos;

/// <summary>
/// Habilidades da BNCC usadas pelo jogo, com o texto oficial (documento "BNCC_EI_EF_110518_versaofinal_site.pdf", MEC).
/// O jogo marca cada pergunta com um destes códigos e envia acertos/erros por habilidade.
///
/// Observações (a BNCC não tem habilidade exata até o 5º ano para estes conteúdos do jogo):
/// - homófonos/parônimos (por que/porque, mau/mal) → EF35LP13 (3º e 4º) e EF05LP01 (5º);
/// - expressões numéricas com parênteses → EF04MA05 (mais próxima: propriedades das operações);
/// - advérbio → EF07LP09 (habilidade do 7º ano, aqui antecipada).
/// </summary>
public static class HabilidadesBncc
{
    public record Habilidade(string Codigo, string Componente, string Ano, string Resumo, string Descricao);

    public static readonly IReadOnlyDictionary<string, Habilidade> Todas = Criar(new[]
    {
        // ===== Língua Portuguesa =====
        new Habilidade("EF01LP08", "Língua Portuguesa", "1º", "Sons e escrita (vogais)",
            "Relacionar elementos sonoros (sílabas, fonemas, partes de palavras) com sua representação escrita."),
        new Habilidade("EF02LP02", "Língua Portuguesa", "2º", "Sílabas",
            "Segmentar palavras em sílabas e remover e substituir sílabas iniciais, mediais ou finais para criar novas palavras."),
        new Habilidade("EF02LP03", "Língua Portuguesa", "2º", "Letras e sons (f, v, t, d, p, b)",
            "Ler e escrever palavras com correspondências regulares diretas entre letras e fonemas (f, v, t, d, p, b) e correspondências regulares contextuais (c e q; e e o, em posição átona em final de palavra)."),
        new Habilidade("EF02LP04", "Língua Portuguesa", "2º", "Vogais em todas as sílabas",
            "Ler e escrever corretamente palavras com sílabas CV, V, CVC, CCV, identificando que existem vogais em todas as sílabas."),
        new Habilidade("EF03LP01", "Língua Portuguesa", "3º", "Ortografia: s/ss, c/qu, r/rr",
            "Ler e escrever palavras com correspondências regulares contextuais entre grafemas e fonemas – c/qu; g/gu; r/rr; s/ss; o (e não u) e e (e não i) em sílaba átona em final de palavra – e com marcas de nasalidade (til, m, n)."),
        new Habilidade("EF03LP03", "Língua Portuguesa", "3º", "Dígrafos lh, nh, ch",
            "Ler e escrever corretamente palavras com os dígrafos lh, nh, ch."),
        new Habilidade("EF04LP01", "Língua Portuguesa", "4º", "Ortografia: regras regulares",
            "Grafar palavras utilizando regras de correspondência fonema-grafema regulares diretas e contextuais."),
        new Habilidade("EF05LP01", "Língua Portuguesa", "5º", "Ortografia: regras e palavras irregulares",
            "Grafar palavras utilizando regras de correspondência fonema-grafema regulares, contextuais e morfológicas e palavras de uso frequente com correspondências irregulares."),
        new Habilidade("EF35LP13", "Língua Portuguesa", "3º ao 5º", "Grafia irregular (x/ch, s/z, ç)",
            "Memorizar a grafia de palavras de uso frequente nas quais as relações fonema-grafema são irregulares e com h inicial que não representa fonema."),
        new Habilidade("EF03LP08", "Língua Portuguesa", "3º", "Substantivos e verbos",
            "Identificar e diferenciar, em textos, substantivos e verbos e suas funções na oração: agente, ação, objeto da ação."),
        new Habilidade("EF03LP09", "Língua Portuguesa", "3º", "Adjetivos",
            "Identificar, em textos, adjetivos e sua função de atribuição de propriedades aos substantivos."),
        new Habilidade("EF35LP14", "Língua Portuguesa", "3º ao 5º", "Pronomes",
            "Identificar em textos e usar na produção textual pronomes pessoais, possessivos e demonstrativos, como recurso coesivo anafórico."),
        new Habilidade("EF05LP07", "Língua Portuguesa", "5º", "Conjunções",
            "Identificar, em textos, o uso de conjunções e a relação que estabelecem entre partes do texto: adição, oposição, tempo, causa, condição, finalidade."),
        new Habilidade("EF07LP09", "Língua Portuguesa", "7º (antecipada)", "Advérbios",
            "Identificar, em textos lidos ou de produção própria, advérbios e locuções adverbiais que ampliam o sentido do verbo núcleo da oração."),
        new Habilidade("EF15LP03", "Língua Portuguesa", "1º ao 5º", "Localizar informações no texto",
            "Localizar informações explícitas em textos."),

        // ===== Matemática =====
        new Habilidade("EF01MA06", "Matemática", "1º", "Fatos básicos da adição",
            "Construir fatos básicos da adição e utilizá-los em procedimentos de cálculo para resolver problemas."),
        new Habilidade("EF02MA05", "Matemática", "2º", "Fatos básicos de adição e subtração",
            "Construir fatos básicos da adição e subtração e utilizá-los no cálculo mental ou escrito."),
        new Habilidade("EF02MA06", "Matemática", "2º", "Problemas de adição e subtração",
            "Resolver e elaborar problemas de adição e de subtração, envolvendo números de até três ordens, com os significados de juntar, acrescentar, separar, retirar, utilizando estratégias pessoais."),
        new Habilidade("EF02MA08", "Matemática", "2º", "Dobro e metade",
            "Resolver e elaborar problemas envolvendo dobro, metade, triplo e terça parte, com o suporte de imagens ou material manipulável, utilizando estratégias pessoais."),
        new Habilidade("EF03MA03", "Matemática", "3º", "Tabuada (fatos da multiplicação)",
            "Construir e utilizar fatos básicos da adição e da multiplicação para o cálculo mental ou escrito."),
        new Habilidade("EF03MA05", "Matemática", "3º", "Adição e subtração",
            "Utilizar diferentes procedimentos de cálculo mental e escrito, inclusive os convencionais, para resolver problemas significativos envolvendo adição e subtração com números naturais."),
        new Habilidade("EF04MA05", "Matemática", "4º", "Propriedades das operações (expressões)",
            "Utilizar as propriedades das operações para desenvolver estratégias de cálculo."),
        new Habilidade("EF04MA07", "Matemática", "4º", "Divisão",
            "Resolver e elaborar problemas de divisão cujo divisor tenha no máximo dois algarismos, envolvendo os significados de repartição equitativa e de medida, utilizando estratégias diversas, como cálculo por estimativa, cálculo mental e algoritmos."),
        new Habilidade("EF05MA03", "Matemática", "5º", "Frações",
            "Identificar e representar frações (menores e maiores que a unidade), associando-as ao resultado de uma divisão ou à ideia de parte de um todo, utilizando a reta numérica como recurso."),
        new Habilidade("EF05MA06", "Matemática", "5º", "Porcentagem",
            "Associar as representações 10%, 25%, 50%, 75% e 100% respectivamente à décima parte, quarta parte, metade, três quartos e um inteiro, para calcular porcentagens, utilizando estratégias pessoais, cálculo mental e calculadora, em contextos de educação financeira, entre outros."),
        new Habilidade("EF05MA07", "Matemática", "5º", "Adição e subtração com decimais (dinheiro)",
            "Resolver e elaborar problemas de adição e subtração com números naturais e com números racionais, cuja representação decimal seja finita, utilizando estratégias diversas, como cálculo por estimativa, cálculo mental e algoritmos."),
        new Habilidade("EF05MA08", "Matemática", "5º", "Problemas de multiplicação e divisão",
            "Resolver e elaborar problemas de multiplicação e divisão com números naturais e com números racionais cuja representação decimal é finita (com multiplicador natural e divisor natural e diferente de zero), utilizando estratégias diversas, como cálculo por estimativa, cálculo mental e algoritmos."),
    });

    private static IReadOnlyDictionary<string, Habilidade> Criar(Habilidade[] lista) =>
        lista.ToDictionary(h => h.Codigo, StringComparer.OrdinalIgnoreCase);

    /// <summary>Abaixo deste percentual de acerto, com pelo menos RespostasMinimas, o aluno aparece em "precisam de atenção".</summary>
    public const double TaxaAtencao = 0.6;
    public const int RespostasMinimas = 4;
}
