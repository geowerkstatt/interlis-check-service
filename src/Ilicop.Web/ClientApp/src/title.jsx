import React from "react";
import InfoCarousel from "./infoCarousel";
import "./app.css";
import { useTranslation } from "react-i18next";

export const Title = (props) => {
  const { clientSettings, customAppLogoPresent, setCustomAppLogoPresent, quickStartContent } = props;
  const { t } = useTranslation();

  return (
    <div className="title-wrapper">
      <div className="app-subtitle">{t("title.title")}</div>
      <div style={{ marginBottom: "2.5rem" }}>
        <img
          className="app-logo"
          src="/app.png"
          alt="App Logo"
          onLoad={() => setCustomAppLogoPresent(true)}
          onError={(e) => (e.target.style.display = "none")}
        />
      </div>
      {!customAppLogoPresent && <div className="app-title">{clientSettings?.applicationName}</div>}
      {quickStartContent && <InfoCarousel content={quickStartContent} />}
    </div>
  );
};
export default Title;
