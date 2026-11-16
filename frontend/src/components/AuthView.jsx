import { ToastList } from './ToastList';

export function AuthView({
  lang,
  toggleLang,
  t,
  authMode,
  setAuthMode,
  authError,
  setAuthError,
  firstName,
  setFirstName,
  lastName,
  setLastName,
  email,
  setEmail,
  password,
  setPassword,
  passwordValidations,
  handleAuthSubmit,
  toasts
}) {
  return (
    <div className="h-screen w-screen overflow-hidden relative flex flex-col justify-center items-center bg-[#070b14] px-4">
      <div className="ambient-soft" />

      <div className="absolute top-6 right-6 z-20">
        <button
          onClick={toggleLang}
          className="text-slate-400 hover:text-white transition-colors text-xs font-medium flex items-center gap-1.5 bg-slate-900/60 hover:bg-slate-800/80 px-3 py-1.5 rounded-lg border border-white/[0.08]"
        >
          <i className="fa-solid fa-globe text-[11px] text-slate-400" />
          <span>{lang === 'TR' ? 'EN' : 'TR'}</span>
        </button>
      </div>

      <div className="w-full max-w-[420px] relative z-10">
        <div className="text-center mb-6">
          <div className="inline-flex items-center justify-center w-12 h-12 rounded-xl bg-slate-900 border border-white/[0.08] mb-3.5 shadow-sm">
            <i className="fa-solid fa-crosshairs text-indigo-400 text-xl" />
          </div>
          <h1 className="text-xl font-bold tracking-tight text-white flex items-center justify-center gap-1.5">
            Seat<span className="text-indigo-400">Hunter</span>
          </h1>
          <p className="text-slate-400 text-xs mt-1 font-normal">{t('loginSubtitle')}</p>
        </div>

        <div className="bg-[#0f172a]/70 border border-white/[0.08] rounded-xl p-6 backdrop-blur-xl shadow-xl">
          {authError && (
            <div className="mb-4 p-3 rounded-lg bg-rose-500/10 text-rose-400 text-xs font-medium border border-rose-500/20 flex items-center gap-2">
              <i className="fa-solid fa-circle-exclamation text-rose-400 text-xs shrink-0" />
              <span>{authError}</span>
            </div>
          )}

          <form onSubmit={handleAuthSubmit} className="space-y-3.5">
            {authMode === "register" && (
              <div className="grid grid-cols-2 gap-2.5">
                <div>
                  <label className="block text-[11px] font-medium text-slate-400 mb-1">{t('name')}</label>
                  <input
                    type="text"
                    value={firstName}
                    onChange={(e) => setFirstName(e.target.value)}
                    required
                    placeholder={t('firstNamePlaceholder')}
                    className="w-full bg-[#070b14]/80 border border-white/[0.08] text-slate-100 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
                  />
                </div>
                <div>
                  <label className="block text-[11px] font-medium text-slate-400 mb-1">{t('surname')}</label>
                  <input
                    type="text"
                    value={lastName}
                    onChange={(e) => setLastName(e.target.value)}
                    required
                    placeholder={t('lastNamePlaceholder')}
                    className="w-full bg-[#070b14]/80 border border-white/[0.08] text-slate-100 rounded-lg px-3 py-2 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
                  />
                </div>
              </div>
            )}

            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1">{t('email')}</label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                placeholder={t('emailPlaceholder')}
                className="w-full bg-[#070b14]/80 border border-white/[0.08] text-slate-100 rounded-lg px-3.5 py-2.5 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
              />
            </div>

            <div>
              <label className="block text-[11px] font-medium text-slate-400 mb-1">{t('password')}</label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                placeholder="••••••••"
                className="w-full bg-[#070b14]/80 border border-white/[0.08] text-slate-100 rounded-lg px-3.5 py-2.5 text-xs outline-none focus:border-indigo-500/80 transition-colors placeholder:text-slate-600"
              />

              {authMode === "register" && (
                <div className="grid grid-cols-2 gap-1.5 mt-2.5 pt-2 border-t border-white/[0.05]">
                  <div className={`flex items-center gap-1.5 text-[10px] ${passwordValidations.length ? 'text-emerald-400' : 'text-slate-500'}`}>
                    <i className={`fa-solid ${passwordValidations.length ? 'fa-check' : 'fa-circle'} text-[8px]`} />
                    <span>{t('pwMinLen')}</span>
                  </div>
                  <div className={`flex items-center gap-1.5 text-[10px] ${passwordValidations.upper ? 'text-emerald-400' : 'text-slate-500'}`}>
                    <i className={`fa-solid ${passwordValidations.upper ? 'fa-check' : 'fa-circle'} text-[8px]`} />
                    <span>{t('pwUpper')}</span>
                  </div>
                  <div className={`flex items-center gap-1.5 text-[10px] ${passwordValidations.lower ? 'text-emerald-400' : 'text-slate-500'}`}>
                    <i className={`fa-solid ${passwordValidations.lower ? 'fa-check' : 'fa-circle'} text-[8px]`} />
                    <span>{t('pwLower')}</span>
                  </div>
                  <div className={`flex items-center gap-1.5 text-[10px] ${passwordValidations.number ? 'text-emerald-400' : 'text-slate-500'}`}>
                    <i className={`fa-solid ${passwordValidations.number ? 'fa-check' : 'fa-circle'} text-[8px]`} />
                    <span>{t('pwNumber')}</span>
                  </div>
                </div>
              )}
            </div>

            <button
              type="submit"
              className="w-full bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg px-4 py-2.5 text-xs transition-colors duration-200 mt-2 shadow-sm flex items-center justify-center gap-2"
            >
              <span>{authMode === "login" ? t('loginBtn') : t('registerBtn')}</span>
              <i className="fa-solid fa-arrow-right text-[10px]" />
            </button>
          </form>

          {authMode === "login" && (
            <div className="mt-4 pt-3 border-t border-white/[0.05] text-center">
              <button
                type="button"
                onClick={() => { setEmail("demo@seathunter.com"); setPassword("Password123!"); }}
                className="text-[11px] text-slate-400 hover:text-indigo-300 transition-colors inline-flex items-center gap-1.5 py-1 px-2 rounded-md hover:bg-white/[0.04]"
              >
                <i className="fa-solid fa-bolt text-amber-400 text-[10px]" />
                <span>{t('demoLoginBtn')}</span>
              </button>
            </div>
          )}
        </div>

        <div className="mt-4 text-center text-xs text-slate-400 font-normal">
          {authMode === "login" ? t('noAccount') : t('hasAccount')}{" "}
          <button
            onClick={() => { setAuthMode(authMode === "login" ? "register" : "login"); setAuthError(""); }}
            className="text-indigo-400 hover:text-indigo-300 font-medium transition-colors ml-1"
          >
            {authMode === "login" ? t('registerBtn') : t('loginBtn')}
          </button>
        </div>
      </div>

      <ToastList toasts={toasts} />
    </div>
  );
}
