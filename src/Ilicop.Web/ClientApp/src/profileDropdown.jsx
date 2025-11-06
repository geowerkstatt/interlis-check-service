import { useCallback, useState, useEffect } from "react";
import { Col, Form, Row } from "react-bootstrap";
import { useTranslation } from "react-i18next";

// Helper function to get display text for a profile in the correct language
const getLocalisedProfileTitle = (profile, language) => {
  if (!profile.titles || profile.titles.length === 0) {
    return profile.id;
  }

  const localTitle = profile.titles.find((title) => title.language === language);
  if (localTitle && localTitle.text) {
    return localTitle.text;
  }

  const fallbackTitle = profile.titles.find((title) => !title.language);
  if (fallbackTitle) {
    return fallbackTitle.text;
  }

  const firstTitle = profile.titles.find((title) => title.text);
  if (firstTitle) {
    return firstTitle.text;
  }

  return profile.id;
};

export const ProfileDropdown = ({ selectedProfile, onProfileChange, disabled = false }) => {
  const [profiles, setProfiles] = useState([]);
  const { i18n, t } = useTranslation();

  // Load profiles from API
  useEffect(() => {
    const loadProfiles = async () => {
      try {
        const response = await fetch("/api/v1/profile");
        if (response.ok) {
          const profileData = await response.json();
          setProfiles(profileData);
        }
      } catch (error) {
        console.error("Failed to load profiles:", error);
      }
    };

    loadProfiles();
  }, []);

  useEffect(() => {
    if (profiles && profiles.length > 0) {
      onProfileChange(profiles[0].id);
    }
  }, [onProfileChange, profiles]);

  const handleChange = useCallback(
    (e) => {
      onProfileChange?.(e.target.value);
    },
    [onProfileChange]
  );

  if (
    profiles === undefined ||
    profiles.length === 0 ||
    profiles.filter((p) => p.id !== "DEFAULT").length === 0 ||
    !selectedProfile
  ) {
    return null;
  } else {
    return (
      <Form.Group as={Row} className="mb-3">
        <Form.Label column>{t("common.profile")}</Form.Label>
        <Col md="10">
          <Form.Control as="select" value={selectedProfile} onChange={handleChange} disabled={disabled}>
            {profiles.map((profile) => (
              <option key={profile.id} value={profile.id}>
                {getLocalisedProfileTitle(profile, i18n.language)}
              </option>
            ))}
          </Form.Control>
        </Col>
      </Form.Group>
    );
  }
};
