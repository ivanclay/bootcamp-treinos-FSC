// Chat do Coach AI: guarda o histórico no sessionStorage e envia a conversa inteira a cada mensagem.
(() => {
  const root = document.querySelector(".chat");
  const list = document.getElementById("messages");
  const form = document.getElementById("composer");
  const input = document.getElementById("input");
  const button = form.querySelector("button");
  const token = root.dataset.token;
  const onboarding = root.dataset.onboarding === "true";
  const storageKey = "fitai.coach.history";

  const welcome = onboarding
    ? [
        "Bem-vindo ao FIT.AI! 🎉",
        "O app que vai transformar a forma como você treina. Aqui você monta seu plano de treino personalizado, acompanha sua evolução com estatísticas detalhadas e conta com uma IA disponível 24h para te guiar em cada exercício.",
        "Tudo pensado para você alcançar seus objetivos de forma inteligente e consistente.",
        "Vamos configurar seu perfil?",
      ]
    : ["Olá! Sou sua IA personal. Como posso ajudar com seu treino hoje?"];

  let history = [];
  try { history = JSON.parse(sessionStorage.getItem(storageKey) || "[]"); } catch { history = []; }

  const escapeHtml = (s) => s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

  function bubble(role, html, videos) {
    const div = document.createElement("div");
    div.className = "msg " + (role === "user" ? "user" : "bot");
    div.innerHTML = html;
    for (const v of videos || []) {
      const a = document.createElement("a");
      a.className = "video-card";
      a.href = v.url;
      a.target = "_blank";
      a.rel = "noopener noreferrer";
      a.innerHTML = `<img src="${escapeHtml(v.thumbnailUrl)}" alt=""><span class="play"><span>▶</span></span><span class="title">${escapeHtml(v.title)}</span>`;
      div.appendChild(a);
    }
    list.appendChild(div);
    list.scrollTop = list.scrollHeight;
    return div;
  }

  function save() {
    try { sessionStorage.setItem(storageKey, JSON.stringify(history.slice(-60))); } catch { /* sem storage */ }
  }

  welcome.forEach((text) => bubble("assistant", `<p>${escapeHtml(text)}</p>`));
  history.forEach((m) => bubble(m.role, m.html || `<p>${escapeHtml(m.content)}</p>`, m.videos));

  if (onboarding && history.length === 0) {
    const start = document.createElement("button");
    start.className = "btn sm";
    start.style.alignSelf = "flex-end";
    start.textContent = "Começar!";
    start.onclick = () => { start.remove(); send("Começar!"); };
    list.appendChild(start);
  }

  async function send(text) {
    text = text.trim();
    if (!text) return;
    history.push({ role: "user", content: text });
    bubble("user", `<p>${escapeHtml(text)}</p>`);
    save();
    input.value = "";
    button.disabled = true;
    const typing = bubble("assistant", '<span class="typing-dots"><span></span><span></span><span></span></span>');
    typing.classList.add("typing");

    try {
      const response = await fetch("/coach/enviar", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json", RequestVerificationToken: token },
        body: JSON.stringify({ messages: history.map((m) => ({ role: m.role, content: m.content })) }),
      });
      const data = await response.json();
      typing.remove();
      if (data.redirect) { window.location.href = data.redirect; return; }
      if (!response.ok) {
        bubble("assistant", `<p>${escapeHtml(data.error || "Não consegui responder agora. Tente de novo.")}</p>`);
        history.pop();
        save();
        return;
      }
      history.push({ role: "assistant", content: data.message, html: data.html, videos: data.videos });
      save();
      bubble("assistant", data.html, data.videos);
      if (data.workoutPlanChanged) {
        const link = document.createElement("a");
        link.className = "btn sm";
        link.style.alignSelf = "flex-start";
        link.href = "/plano";
        link.textContent = "Ver meu plano de treino";
        list.appendChild(link);
        list.scrollTop = list.scrollHeight;
      }
    } catch {
      typing.remove();
      bubble("assistant", "<p>Sem conexão com o servidor. Tente de novo.</p>");
      history.pop();
      save();
    } finally {
      button.disabled = false;
      input.focus();
    }
  }

  form.addEventListener("submit", (e) => { e.preventDefault(); send(input.value); });
  document.querySelectorAll("#suggestions button").forEach((b) => b.addEventListener("click", () => send(b.textContent)));

  const invite = document.getElementById("invite-form");
  if (invite) {
    invite.addEventListener("submit", async (e) => {
      e.preventDefault();
      const code = invite.code.value.trim();
      if (!code) return;
      const response = await fetch("/coach/codigo", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json", RequestVerificationToken: token },
        body: JSON.stringify({ code }),
      });
      const data = await response.json();
      if (response.ok) {
        invite.remove();
        bubble("assistant", `<p>Pronto! Agora você é acompanhado por <strong>${escapeHtml(data.teacherName)}</strong>. 💪</p>`);
      } else {
        bubble("assistant", `<p>${escapeHtml(data.error || "Código inválido.")}</p>`);
      }
    });
  }

  if (root.dataset.prefill) send(root.dataset.prefill);
})();
