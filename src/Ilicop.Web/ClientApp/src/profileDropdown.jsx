import { useCallback, useState, useEffect } from "react";
import { Col, Form, Row } from "react-bootstrap";

export const ProfileDropdown = ({ selectedProfile, onProfileChange, disabled = false }) => {
  const [profiles, setProfiles] = useState([]);

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

  // Helper function to get display text for a profile
  const getProfileDisplayText = useCallback((profile) => {
    if (!profile.titles || profile.titles.length === 0) {
      return profile.id;
    }

    // Look for German title first
    const germanTitle = profile.titles.find((title) => title.language === "de");
    if (germanTitle) {
      return germanTitle.text || profile.id;
    }

    // Fallback to title with no language or empty language
    const fallbackTitle = profile.titles.find(
      (title) => title.language === null || title.language === "" || title.language === undefined
    );
    if (fallbackTitle) {
      return fallbackTitle.text;
    }

    // Final fallback to profile ID
    return profile.id;
  }, []);

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
        <Form.Label column>Profil</Form.Label>
        <Col md="10">
          <Form.Control as="select" value={selectedProfile} onChange={handleChange} disabled={disabled}>
            {profiles.map((profile) => (
              <option key={profile.id} value={profile.id}>
                {getProfileDisplayText(profile)}
              </option>
            ))}
          </Form.Control>
        </Col>
      </Form.Group>
    );
  }
};
