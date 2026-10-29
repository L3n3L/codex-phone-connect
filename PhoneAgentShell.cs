namespace CodexPhoneConnect;

/// <summary>
/// 正式手机页的目标选择和主题层。
/// 触摸粒子算法仍由 prototype/phone-scroll.html 拥有；本壳层只复用样本的视觉变量并切换外观。
/// </summary>
public static class PhoneAgentShell
{
    public static string BuildMarkup()
    {
        var buttons = string.Join(Environment.NewLine, AgentCatalog.All.Select(agent =>
        {
            var id = System.Net.WebUtility.HtmlEncode(agent.WireId);
            var name = System.Net.WebUtility.HtmlEncode(agent.DisplayName);
            var icon = System.Net.WebUtility.HtmlEncode(agent.IconPhoneAsset);
            return $$"""
        <button type="button" data-agent="{{id}}" aria-label="选择 {{name}}" aria-pressed="false">
          <img src="/assets/brand/{{icon}}" alt="" />
          <span>{{name}}</span>
        </button>
        """;
        }));
        var agents = System.Text.Json.JsonSerializer.Serialize(AgentCatalog.All.Select(agent => new
        {
            id = agent.WireId,
            name = agent.DisplayName,
        }));

        return """
      <div id="agent-switcher" aria-label="选择智能体">
        {{AGENT_BUTTONS}}
      </div>
      <script>const agentCatalog = {{AGENT_CATALOG}};</script>
      <style>
        /* 这些变量直接对应 prototype/agent-style-preview.html 的手机样本。 */
        html, body {
          --phone-bg: #ffffff;
          --phone-ink: #30322f;
          --phone-muted: #92958e;
          --phone-line: #e8e9e4;
          --particle-glow: rgba(103, 161, 205, .18);
          --send-bg: #30322f;
          --send-ink: #ffffff;
          transition: background 520ms ease, color 520ms ease;
        }

        html[data-agent-theme="claude"],
        body[data-agent-theme="claude"] {
          color-scheme: dark;
          --phone-bg: #090b0d;
          --phone-ink: #f4f0e8;
          --phone-muted: #9e9b94;
          --phone-line: #2a2e31;
          --particle-glow: rgba(220, 140, 96, .20);
          --send-bg: #f0e6d8;
          --send-ink: #171715;
        }

        html[data-agent-theme="workbuddy"],
        body[data-agent-theme="workbuddy"] {
          color-scheme: light;
          --phone-bg: #fbfefd;
          --phone-ink: #263b35;
          --phone-muted: #82938c;
          --phone-line: #dcebe5;
          --particle-glow: rgba(74, 204, 158, .22);
          --send-bg: #218d74;
          --send-ink: #ffffff;
        }

        html[data-agent-theme="dsh"],
        body[data-agent-theme="dsh"] {
          color-scheme: light;
          --phone-bg: #fcfcfd;
          --phone-ink: #383a3e;
          --phone-muted: #898b90;
          --phone-line: #e2e3e6;
          --particle-glow: rgba(120, 123, 130, .2);
          --send-bg: #5b5e65;
          --send-ink: #ffffff;
        }

        html[data-agent-theme="gpt"],
        body[data-agent-theme="gpt"],
        html[data-agent-theme="workbuddy"],
        body[data-agent-theme="workbuddy"],
        html[data-agent-theme="dsh"],
        body[data-agent-theme="dsh"] {
          color-scheme: light;
          background: var(--phone-bg) !important;
          color: var(--phone-ink) !important;
        }

        html[data-agent-theme="claude"],
        body[data-agent-theme="claude"] {
          background: var(--phone-bg) !important;
          color: var(--phone-ink) !important;
        }

        body[data-agent-theme] .remote-field::after {
          content: "";
          position: absolute;
          z-index: 1;
          inset: 0;
          pointer-events: none;
          background: radial-gradient(ellipse at 50% 55%, var(--particle-glow), transparent 68%);
          filter: blur(12px);
          opacity: .9;
          transition: background 520ms ease, opacity 520ms ease;
        }

        body[data-agent-theme] #field-canvas {
          z-index: 0;
          transform: translateY(6%);
          filter: saturate(1.18) brightness(1.08);
          transition: filter 520ms ease, transform 520ms ease;
        }

        body[data-agent-theme="claude"] #field-canvas {
          /* 让现有粒子整体进入样本的暖橙色域，不改变粒子运动和触摸响应。 */
          filter: sepia(.54) saturate(1.75) hue-rotate(328deg) brightness(1.13) contrast(1.05);
        }

        body[data-agent-theme="workbuddy"] #field-canvas {
          /* 品牌主题只通过画布滤镜着色，不改变原粒子的运动和触摸算法。 */
          filter: hue-rotate(24deg) saturate(1.32) brightness(1.1);
        }

        body[data-agent-theme="workbuddy"] .remote-field::after {
          background: radial-gradient(
            ellipse at 50% 48%,
            rgba(66, 211, 163, .25) 0%,
            rgba(104, 224, 180, .14) 38%,
            rgba(176, 239, 211, .07) 58%,
            transparent 74%);
        }

        body[data-agent-theme="dsh"] #field-canvas {
          /* DSH 的中性灰配色只改画布滤镜，不触碰粒子运动和触摸算法。 */
          filter: grayscale(.9) saturate(.16) contrast(1.04) brightness(1.08);
        }

        body[data-agent-theme="dsh"] .remote-field::after {
          background: radial-gradient(
            ellipse at 50% 48%,
            rgba(83, 86, 93, .23) 0%,
            rgba(145, 148, 154, .14) 40%,
            rgba(204, 206, 210, .07) 60%,
            transparent 75%);
        }

        body[data-agent-theme] .field-hint { z-index: 2; color: var(--phone-muted) !important; }
        body[data-agent-theme] .connection {
          top: max(15px, env(safe-area-inset-top));
          right: 15px;
          min-height: 40px;
          display: flex;
          align-items: center;
          color: var(--phone-muted) !important;
          transition: color 520ms ease;
        }

        body[data-agent-theme] .connection::before {
          flex: 0 0 7px;
          vertical-align: unset;
        }
        body[data-agent-theme] .status { color: var(--phone-muted) !important; }
        body[data-agent-theme] .status strong { color: var(--phone-ink) !important; }

        body[data-agent-theme] .composer {
          border-color: var(--phone-line) !important;
          background: var(--phone-bg) !important;
          background: color-mix(in srgb, var(--phone-bg) 92%, transparent) !important;
          box-shadow: 0 7px 22px color-mix(in srgb, var(--phone-ink) 7%, transparent) !important;
          transition: background 520ms ease, border-color 520ms ease, box-shadow 520ms ease;
        }

        body[data-agent-theme] .composer textarea {
          color: var(--phone-ink) !important;
        }

        body[data-agent-theme] .composer textarea::placeholder { color: var(--phone-muted) !important; }

        body[data-agent-theme] .composer > button {
          border-color: transparent !important;
          background: var(--send-bg) !important;
          color: var(--send-ink) !important;
          transition: background 520ms ease, color 520ms ease, transform 160ms ease;
        }

        body[data-agent-theme="workbuddy"] .composer > button {
          background: linear-gradient(135deg, #39c99f 0%, #15977c 100%) !important;
        }

        body[data-agent-theme="dsh"] .composer > button {
          background: linear-gradient(135deg, #858990 0%, #555960 100%) !important;
        }

        #agent-switcher {
          position: fixed;
          z-index: 20;
          top: max(12px, env(safe-area-inset-top));
          left: 12px;
          right: 12px;
          display: flex;
          gap: 3px;
          padding: 3px;
          border: 1px solid var(--phone-line);
          border-radius: 10px;
          background: color-mix(in srgb, var(--phone-bg) 82%, transparent);
          box-shadow: 0 7px 22px color-mix(in srgb, var(--phone-ink) 7%, transparent);
          backdrop-filter: blur(12px);
          pointer-events: none;
          transition: background 520ms ease, border-color 520ms ease, box-shadow 520ms ease;
        }

        #agent-switcher button {
          flex: 1 1 0;
          min-width: 0;
          height: 34px;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          gap: 4px;
          padding: 0 2px;
          border: 0;
          border-radius: 7px;
          background: transparent;
          color: var(--phone-muted);
          font: inherit;
          font-size: 11px;
          pointer-events: auto;
          cursor: pointer;
          transition: background 220ms ease, color 220ms ease, transform 160ms ease;
        }

        #agent-switcher button:active { transform: scale(.96); }
        #agent-switcher button.is-active { background: color-mix(in srgb, var(--phone-ink) 8%, transparent); color: var(--phone-ink); }
        #agent-switcher img { width: 17px; height: 17px; flex: 0 0 17px; object-fit: contain; }
        #agent-switcher button[data-agent="gpt"] img { filter: brightness(.45); }

        body[data-agent-theme="claude"] #agent-switcher button[data-agent="gpt"] img { filter: brightness(0) invert(1); }
        body[data-agent-theme="claude"] #agent-switcher button[data-agent="claude"] img { filter: saturate(.85) brightness(1.05); }
        body[data-agent-theme="claude"] #agent-switcher button[data-agent="workbuddy"] img { filter: saturate(.85) brightness(1.05); }

        body[data-agent-theme] .connection {
          top: max(58px, calc(env(safe-area-inset-top) + 58px));
          right: 15px;
        }
      </style>
      <script>
        (() => {
          const root = document.getElementById('agent-switcher');
          if (!root) return;
          const buttons = [...root.querySelectorAll('[data-agent]')];
          const connection = document.querySelector('.connection');
          const composerInput = document.querySelector('.composer textarea');
          const remoteField = document.getElementById('remote-field');
          const fieldHint = document.querySelector('.field-hint');
          const defaultAgent = agentCatalog[0]?.id || 'gpt';
          const knownAgents = new Set(agentCatalog.map((agent) => agent.id));
          const agentNames = Object.fromEntries(agentCatalog.map((agent) => [agent.id, agent.name]));
          const state = { selectedAgent: defaultAgent, statusKnown: false, userSelected: false };

          // The frozen page still calls /api/jobs and /api/control/scroll without
          // an agentId. Keep that page unchanged and add the explicit target at
          // the integration boundary instead.
          const nativeFetch = window.fetch.bind(window);
          const isTargetRequest = (input) => {
            const rawUrl = typeof input === 'string' ? input : input && input.url;
            if (!rawUrl) return false;
            const path = new URL(rawUrl, window.location.href).pathname;
            return path === '/api/jobs' || path === '/api/control/scroll';
          };

          window.fetch = (input, init) => {
            if (isTargetRequest(input) && init && typeof init.body === 'string') {
              try {
                const payload = JSON.parse(init.body);
                if (payload && typeof payload === 'object' && !Array.isArray(payload)
                    && (state.statusKnown || state.userSelected)
                    && !Object.prototype.hasOwnProperty.call(payload, 'agentId')) {
                  init = {
                    ...init,
                    body: JSON.stringify({ ...payload, agentId: state.selectedAgent })
                  };
                }
              } catch (_) {
                // Leave non-JSON requests untouched; the original page owns them.
              }
            }
            return nativeFetch(input, init);
          };

          const render = (agent, fromUser = false) => {
            const selected = knownAgents.has(agent) ? agent : defaultAgent;
            if (fromUser) state.userSelected = true;
            state.selectedAgent = selected;
            root.dataset.agent = selected;
            document.documentElement.dataset.agentTheme = selected;
            document.body.dataset.agentTheme = selected;
            const targetName = agentNames[selected];
            if (connection) connection.textContent = `${targetName} · 已连接`;
            if (composerInput) composerInput.placeholder = `输入要发送给 ${targetName} 的内容`;
            if (remoteField) remoteField.setAttribute('aria-label', `${targetName} 滑动遥控区`);
            if (fieldHint) fieldHint.textContent = `上下滑动控制 ${targetName}`;
            buttons.forEach((button) => {
              button.classList.toggle('is-active', button.dataset.agent === selected);
              button.setAttribute('aria-selected', button.dataset.agent === selected ? 'true' : 'false');
            });
          };

          const choose = async (agent) => {
            render(agent, true);
            buttons.forEach((button) => { button.disabled = true; });
            try {
              const response = await fetch('/api/control/agent', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ agentId: agent })
              });
              if (!response.ok) throw new Error('agent_switch_failed');
            } catch (_) {
              try {
                const response = await fetch('/api/status');
                if (response.ok) render((await response.json()).activeAgent || defaultAgent);
              } catch (_) { render(defaultAgent); }
            } finally {
              buttons.forEach((button) => { button.disabled = false; });
            }
          };

          buttons.forEach((button) => {
            button.addEventListener('click', () => choose(button.dataset.agent));
          });

          fetch('/api/status')
            .then((response) => response.ok ? response.json() : null)
            .then((status) => {
              if (status) {
                render(status.activeAgent || defaultAgent);
                state.statusKnown = true;
              }
            })
            .catch(() => {});
        })();
      </script>
      """
        .Replace("{{AGENT_BUTTONS}}", buttons, StringComparison.Ordinal)
        .Replace("{{AGENT_CATALOG}}", agents, StringComparison.Ordinal);
    }
}
