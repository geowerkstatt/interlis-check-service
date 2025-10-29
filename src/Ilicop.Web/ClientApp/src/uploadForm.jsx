import { useCallback } from "react";
import { Button, Card, Col, Form, Row } from "react-bootstrap";
import { ProfileDropdown } from "./profileDropdown";
import { Trans, useTranslation } from "react-i18next";

export const UploadForm = ({
  nutzungsbestimmungenAvailable,
  checkedNutzungsbestimmungen,
  showNutzungsbestimmungen,
  setCheckedNutzungsbestimmungen,
  validationRunning,
  startValidation,
  resetForm,
  selectedProfile,
  setSelectedProfile,
}) => {
  const { t } = useTranslation();

  const onChangeNutzungsbestimmungen = useCallback(
    (e) => {
      setCheckedNutzungsbestimmungen(e.target.checked);
    },
    [setCheckedNutzungsbestimmungen]
  );

  return (
    <Row>
      <Col>
        <Card>
          <Card.Body>
            <Form>
              {nutzungsbestimmungenAvailable && (
                <Form.Group className="mb-3">
                  <Form.Check>
                    <Form.Check.Input
                      onChange={onChangeNutzungsbestimmungen}
                      checked={checkedNutzungsbestimmungen}
                      disabled={validationRunning}
                    />
                    <Form.Check.Label>
                      <Trans i18nKey="uploadForm.acceptTerms">
                        Ich akzeptiere die
                        <b type="button" onClick={showNutzungsbestimmungen}>
                          Nutzungsbedingungen
                        </b>
                      </Trans>
                    </Form.Check.Label>
                  </Form.Check>
                </Form.Group>
              )}

              <ProfileDropdown
                selectedProfile={selectedProfile}
                onProfileChange={setSelectedProfile}
                disabled={validationRunning}
              />

              <Row>
                <Col className="d-grid">
                  <Button variant="outline-dark" onClick={resetForm}>
                    {t("common.cancel")}
                  </Button>
                </Col>
                <Col className="d-grid">
                  {validationRunning ? (
                    <Button className="check-button" disabled>
                      {t("uploadForm.validationRunning")}
                    </Button>
                  ) : (
                    <Button
                      className="check-button"
                      onClick={startValidation}
                      disabled={nutzungsbestimmungenAvailable && !checkedNutzungsbestimmungen}
                    >
                      {t("common.validate")}
                    </Button>
                  )}
                </Col>
              </Row>
            </Form>
          </Card.Body>
        </Card>
      </Col>
    </Row>
  );
};
