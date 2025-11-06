import "./app.css";
import React from "react";
import About from "./about";
import { Button } from "react-bootstrap";
import { useTranslation } from "react-i18next";

export const Footer = (props) => {
  const {
    openModalContent,
    infoHilfeContent,
    nutzungsbestimmungenContent,
    datenschutzContent,
    impressumContent,
    clientSettings,
    licenseInfoCustom,
    licenseInfo,
  } = props;

  const { t } = useTranslation();

  return (
    <footer className="footer-style">
      {infoHilfeContent && (
        <Button
          variant="link"
          className="footer-button no-outline-on-focus"
          onClick={() => openModalContent(infoHilfeContent, "markdown")}
        >
          {t("footer.infoHelp").toUpperCase()}
        </Button>
      )}
      {nutzungsbestimmungenContent && (
        <Button
          variant="link"
          className="footer-button no-outline-on-focus"
          onClick={() => openModalContent(nutzungsbestimmungenContent, "markdown")}
        >
          {t("footer.termsOfUse").toUpperCase()}
        </Button>
      )}
      {datenschutzContent && (
        <Button
          variant="link"
          className="footer-button no-outline-on-focus"
          onClick={() => openModalContent(datenschutzContent, "markdown")}
        >
          {t("footer.privacy").toUpperCase()}
        </Button>
      )}
      {impressumContent && (
        <Button
          variant="link"
          className="footer-button no-outline-on-focus"
          onClick={() => openModalContent(impressumContent, "markdown")}
        >
          {t("footer.imprint").toUpperCase()}
        </Button>
      )}
      <Button
        variant="link"
        className="footer-button no-outline-on-focus"
        onClick={() =>
          openModalContent(
            <About clientSettings={clientSettings} licenseInfo={{ ...licenseInfoCustom, ...licenseInfo }} />,
            "raw"
          )
        }
      >
        {t("footer.about").toUpperCase()}
      </Button>
    </footer>
  );
};

export default Footer;
