import i18n from "i18next";
import backend from "i18next-http-backend";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";

export const SupportedLanguages = ["de", "fr"];

i18n
  .use(backend)
  .use(initReactI18next)
  .use(LanguageDetector)
  .init({
    detection: {
      order: ["cookie", "navigator", "htmlTag"],
      lookupCookie: "i18next",
      caches: ["cookie"],
    },
    backend: {
      loadPath: `/locale/{{lng}}/{{ns}}.json`,
      allowMultiLoading: false,
    },
    react: {
      useSuspense: false,
    },
    supportedLngs: SupportedLanguages,
    fallbackLng: "de",
    ns: ["common"],
    defaultNS: "common",
    interpolation: {
      escapeValue: false,
      formatSeparator: ",",
      transSupportBasicHtmlNodes: true,
    },
  });

export default i18n;
