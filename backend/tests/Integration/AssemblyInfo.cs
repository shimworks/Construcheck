using Xunit;

// Todas as classes deste projeto sobem um host ASP.NET Core real via
// WebApplicationFactory<Program> (CustomWebApplicationFactory). Program.cs configura
// o Serilog com CreateBootstrapLogger(), que produz um ReloadableLogger atribuído ao
// campo ESTÁTICO Log.Logger — compartilhado por todas as instâncias de host no mesmo
// processo dotnet test. Esse logger é projetado para ser configurado uma única vez e
// depois "congelado" (Freeze()) quando builder.Host.UseSerilog(...) assume.
//
// O xUnit roda classes de teste diferentes em paralelo por padrão. Como cada classe
// de teste de integração tem sua própria instância de CustomWebApplicationFactory,
// que por sua vez sobe seu próprio host, duas ou mais instâncias de host podem tentar
// congelar o MESMO Log.Logger estático ao mesmo tempo. A primeira a chegar congela
// com sucesso; qualquer outra que colida na mesma janela de tempo recebe
// "InvalidOperationException: The logger is already frozen." — não determinístico,
// depende de qual classe de teste o xUnit decide agendar em paralelo com qual outra.
//
// A correção correta aqui é desabilitar paralelismo ENTRE CLASSES deste assembly, não
// alterar Program.cs — nenhuma classe deste projeto se beneficiaria de paralelismo
// real, já que todas competem pelo mesmo recurso estático global do processo host.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
