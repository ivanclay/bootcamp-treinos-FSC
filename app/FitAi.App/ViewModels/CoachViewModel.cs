using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;
using Markdig;

namespace FitAi.App.ViewModels;

public sealed class ChatItem
{
    public required bool IsUser { get; init; }
    public required string Content { get; init; }

    /// <summary>HTML simples (negrito, listas, links) para Label com TextType=Html.</summary>
    public string Html { get; init; } = "";

    public IReadOnlyList<ExerciseVideoResponse> Videos { get; init; } = [];

    public bool IsBot => !IsUser;
    public bool HasVideos => Videos.Count > 0;
}

public partial class CoachViewModel(ApiClient api, AuthService auth) : BaseViewModel, IQueryAttributable
{
    private static readonly MarkdownPipeline Markdown = new MarkdownPipelineBuilder().DisableHtml().Build();

    /// <summary>Pergunta enviada automaticamente ao abrir o chat (ex.: dúvida sobre um exercício).</summary>
    public static string? PendingQuestion { get; set; }

    private bool _onboarding;

    public ObservableCollection<ChatItem> Messages { get; } = [];

    public IReadOnlyList<string> Suggestions { get; } =
        ["Montar meu plano de treino", "Alterar plano de treino", "Mudar objetivo", "Atualizar meus dados"];

    [ObservableProperty] public partial string Input { get; set; } = "";
    [ObservableProperty] public partial bool IsSending { get; set; }
    [ObservableProperty] public partial bool ShowInvite { get; set; }
    [ObservableProperty] public partial string InviteCode { get; set; } = "";
    [ObservableProperty] public partial bool PlanChanged { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _onboarding = query.TryGetValue("onboarding", out var value) && value?.ToString() == "true";

    [RelayCommand]
    private async Task AppearingAsync()
    {
        if (Messages.Count == 0)
        {
            var welcome = _onboarding
                ? new[]
                {
                    "Bem-vindo ao FIT.AI! 🎉",
                    "O app que vai transformar a forma como você treina. Aqui você monta seu plano de treino personalizado, acompanha sua evolução com estatísticas detalhadas e conta com uma IA disponível 24h para te guiar em cada exercício.",
                    "Vamos configurar seu perfil?",
                }
                : ["Olá! Sou sua IA personal. Como posso ajudar com seu treino hoje?"];
            foreach (var text in welcome) Messages.Add(Bot(text, []));
        }

        var user = auth.User;
        ShowInvite = user is { Role: UserRole.STUDENT, TeacherId: null };

        if (PendingQuestion is { } question)
        {
            PendingQuestion = null;
            await SendTextAsync(question);
        }
    }

    [RelayCommand]
    private Task SendAsync() => SendTextAsync(Input);

    [RelayCommand]
    private Task SuggestionAsync(string text) => SendTextAsync(text);

    [RelayCommand]
    private async Task OpenVideoAsync(ExerciseVideoResponse? video)
    {
        if (video is not null) await Browser.Default.OpenAsync(video.Url, BrowserLaunchMode.External);
    }

    [RelayCommand]
    private Task OpenPlanAsync() => Shell.Current.GoToAsync("//main/plan");

    [RelayCommand]
    private Task RedeemInviteAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(InviteCode)) return;
        var link = await api.RedeemInviteCodeAsync(InviteCode.Trim());
        await auth.RefreshUserAsync();
        ShowInvite = false;
        Messages.Add(Bot($"Pronto! Agora você é acompanhado por **{link.TeacherName}**. 💪", []));
    });

    private async Task SendTextAsync(string? text)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || IsSending) return;

        Messages.Add(new ChatItem { IsUser = true, Content = text, Html = System.Net.WebUtility.HtmlEncode(text) });
        Input = "";
        IsSending = true;
        ErrorMessage = null;
        try
        {
            // Envia só a conversa real (as boas-vindas locais não vão para a IA).
            var history = Messages
                .SkipWhile(m => m.IsBot)
                .Select(m => new CoachMessage { Role = m.IsUser ? "user" : "assistant", Content = m.Content })
                .ToList();
            var response = await api.SendCoachMessageAsync(new CoachChatRequest { Messages = history });
            Messages.Add(Bot(response.Message, response.Videos));
            if (response.WorkoutPlanChanged)
            {
                PlanChanged = true;
                await auth.RefreshUserAsync();
            }
        }
        catch (Exception e)
        {
            Messages.Add(Bot(Format.ErrorMessage(e), []));
        }
        finally
        {
            IsSending = false;
        }
    }

    private static ChatItem Bot(string markdown, IReadOnlyList<ExerciseVideoResponse> videos) => new()
    {
        IsUser = false,
        Content = markdown,
        Html = Markdig.Markdown.ToHtml(markdown, Markdown).Trim(),
        Videos = videos,
    };
}
