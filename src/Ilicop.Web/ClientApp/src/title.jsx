import React from "react";
import InfoCarousel from "./infoCarousel";
import "./app.css";
import { useTranslation } from "react-i18next";

const DEFAULT_APP_LOGO = "/app.png";

// Prefer a language-specific logo (e.g. /app.fr.png) and fall back to /app.png
const localizedAppLogo = (language) => (language ? `/app.${language}.png` : DEFAULT_APP_LOGO);

export const Title = (props) => {
  const { clientSettings, customAppLogoPresent, setCustomAppLogoPresent, quickStartContent } = props;
  const { t, i18n } = useTranslation();

  const showAppLogo = (e) => {
    e.target.style.display = "";
    setCustomAppLogoPresent(true);
  };

  const fallBackToDefaultAppLogo = (e) => {
    if (e.target.src.endsWith(DEFAULT_APP_LOGO)) {
      e.target.style.display = "none";
      setCustomAppLogoPresent(false);
    } else {
      e.target.src = DEFAULT_APP_LOGO;
    }
  };

  return (
    <div className="title-wrapper">
      <div className="app-subtitle">{t("title.title")}</div>
      <div style={{ marginBottom: "2.5rem" }}>
        <img
          className="app-logo"
          src={localizedAppLogo(i18n.language)}
          alt="App Logo"
          onLoad={showAppLogo}
          onError={fallBackToDefaultAppLogo}
        />
      </div>
      {!customAppLogoPresent && <div className="app-title">{clientSettings?.applicationName}</div>}
      {quickStartContent && <InfoCarousel content={quickStartContent} />}
    </div>
  );
};
export default Title;
