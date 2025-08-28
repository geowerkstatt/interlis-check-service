import { useCallback } from "react";
import { Button, Card, Col, Form, Row } from "react-bootstrap";
import { ProfileDropdown } from "./profileDropdown";

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
                      Ich akzeptiere die{" "}
                      <b type="button" onClick={showNutzungsbestimmungen}>
                        Nutzungsbedingungen
                      </b>
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
                    Abbrechen
                  </Button>
                </Col>
                <Col className="d-grid">
                  {validationRunning ? (
                    <Button className="check-button" disabled>
                      Validierung läuft...
                    </Button>
                  ) : (
                    <Button
                      className="check-button"
                      onClick={startValidation}
                      disabled={nutzungsbestimmungenAvailable && !checkedNutzungsbestimmungen}
                    >
                      Validieren
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
