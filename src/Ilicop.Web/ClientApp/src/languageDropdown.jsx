import { Dropdown, DropdownButton } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import { SupportedLanguages } from "./i18n";

export const LanguageDropdown = () => {
  const { i18n } = useTranslation();

  return (
    <>
      <DropdownButton title={i18n.language?.toUpperCase() || ""} variant="secondary">
        {SupportedLanguages.map((lang) => (
          <Dropdown.Item key={lang} onClick={() => i18n.changeLanguage(lang)}>
            {lang.toUpperCase()}
          </Dropdown.Item>
        ))}
      </DropdownButton>
    </>
  );
};

export default LanguageDropdown;
