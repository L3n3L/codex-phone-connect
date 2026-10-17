namespace CodexPhoneConnect;

/// <summary>
/// 正式手机页的目标选择和主题层。
/// 触摸粒子算法仍由 prototype/phone-scroll.html 拥有；本壳层只复用样本的视觉变量并切换外观。
/// </summary>
public static class PhoneAgentShell
{
    public const string Markup = """
      <div id="agent-switcher" aria-label="选择智能体">
        <button type="button" data-agent="gpt" aria-label="选择 GPT">
          <img src="/assets/brand/openai-symbol.svg" alt="" />
          <span>GPT</span>
        </button>
        <button type="button" data-agent="claude" aria-label="选择 Claude">
          <img src="/assets/brand/claude.svg" alt="" />
          <span>Claude</span>
        </button>
      </div>
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

        html[data-agent-theme="gpt"],
        body[data-agent-theme="gpt"] {
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

        #agent-switcher {
          position: fixed;
          z-index: 20;
          top: max(15px, env(safe-area-inset-top));
          left: 15px;
          display: inline-flex;
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
          width: 77px;
          min-width: 77px;
          height: 34px;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          gap: 6px;
          padding: 0;
          border: 0;
          border-radius: 7px;
          background: transparent;
          color: var(--phone-muted);
          font: inherit;
          font-size: 12px;
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
          const state = { selectedAgent: 'gpt', statusKnown: false, userSelected: false };

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
            const selected = agent === 'claude' ? 'claude' : 'gpt';
            if (fromUser) state.userSelected = true;
            state.selectedAgent = selected;
            root.dataset.agent = selected;
            document.documentElement.dataset.agentTheme = selected;
            document.body.dataset.agentTheme = selected;
            const targetName = selected === 'claude' ? 'Claude' : 'Codex';
            if (connection) connection.textContent = selected === 'claude' ? 'Claude · 已连接' : 'GPT · 已连接';
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
                if (response.ok) render((await response.json()).activeAgent || 'gpt');
              } catch (_) { render('gpt'); }
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
                render(status.activeAgent || 'gpt');
                state.statusKnown = true;
              }
            })
            .catch(() => {});
        })();
      </script>
      """;
}
