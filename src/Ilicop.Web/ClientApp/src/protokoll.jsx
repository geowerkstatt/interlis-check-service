import "./app.css";
import React, { useState, useRef, useEffect } from "react";
import DayJS from "dayjs";
import { Card, Col, Container, Row } from "react-bootstrap";
import { GoFile, GoFileCode } from "react-icons/go";
import { BsGeoAlt, BsLink45Deg } from "react-icons/bs";
import { LogDisplay } from "./logDisplay";

export const Protokoll = (props) => {
  const { log, statusData, fileName, validationRunning } = props;
  const copyToClipboardTooltipDefaultText = "XTF-Log-Datei Link in die Zwischenablage kopieren";
  const [copyToClipboardTooltipText, setCopyToClipboardTooltipText] = useState(copyToClipboardTooltipDefaultText);
  const [indicateWaiting, setIndicateWaiting] = useState(false);
  const protokollTimestamp = DayJS(new Date()).format("YYYYMMDDHHmm");
  const protokollFileName = "Ilivalidator_output_" + fileName + "_" + protokollTimestamp;
  const logEndRef = useRef(null);

  // Autoscroll protokoll log
  useEffect(() => logEndRef.current?.scrollIntoView({ behavior: "smooth" }), [log]);

  // Show flash dot to indicate waiting
  useEffect(() =>
    setTimeout(() => {
      if (validationRunning === true) {
        setIndicateWaiting(!indicateWaiting);
      } else {
        setIndicateWaiting(false);
      }
    }, 500)
  );

  // Copy to clipboard
  const resetToDefaultText = () => setCopyToClipboardTooltipText(copyToClipboardTooltipDefaultText);
  const currentUrl = window.location.toString();
  const copyToClipboard = () => {
    navigator.clipboard.writeText(currentUrl.slice(0, currentUrl.length - 1) + statusData.xtfLogUrl);
    setCopyToClipboardTooltipText("Link wurde kopiert");
  };

  const statusClass = statusData && statusData.status === "completed" ? "valid" : "errors";
  const statusText = statusData && statusData.status === "completed" ? "Keine Fehler!" : "Fehler!";

  return (
    <Container>
      {log.length > 0 && (
        <Row className="g-5">
          <Col>
            <Card className="protokoll-card">
              <Card.Body>
                <div className="protokoll">
                  {log.map((logEntry, index) => (
                    <div key={index}>
                      {logEntry}
                      {indicateWaiting && index === log.length - 1 && "."}
                    </div>
                  ))}
                  <div ref={logEndRef} />
                </div>
                {statusData && (
                  <Card.Title className={`status ${statusClass}`}>
                    {statusText}
                    <span>
                      {statusData.logUrl && (
                        <span className="icon-tooltip">
                          <a
                            download={protokollFileName + ".log"}
                            className={statusClass + " download-icon"}
                            href={statusData.logUrl}
                          >
                            <GoFile />
                          </a>
                          <span className="icon-tooltip-text">Log-Datei herunterladen</span>
                        </span>
                      )}
                      {statusData.xtfLogUrl && (
                        <span className="icon-tooltip">
                          <a
                            download={protokollFileName + ".xtf"}
                            className={statusClass + " download-icon"}
                            href={statusData.xtfLogUrl}
                          >
                            <GoFileCode />
                          </a>
                          <span className="icon-tooltip-text">XTF-Log-Datei herunterladen</span>
                        </span>
                      )}
                      {statusData.xtfLogUrl && (
                        <span className="icon-tooltip">
                          <div
                            className={statusClass + " btn-sm download-icon"}
                            onClick={copyToClipboard}
                            onMouseLeave={resetToDefaultText}
                          >
                            <BsLink45Deg />
                            <span className="icon-tooltip-text">{copyToClipboardTooltipText}</span>
                          </div>
                        </span>
                      )}
                      {statusData.geoJsonLogUrl && (
                        <span className="icon-tooltip">
                          <a
                            download={protokollFileName + ".geojson"}
                            className={statusClass + " download-icon"}
                            href={statusData.geoJsonLogUrl}
                          >
                            <BsGeoAlt />
                          </a>
                          <span className="icon-tooltip-text">
                            Positionsbezogene Log-Daten als GeoJSON-Datei herunterladen
                          </span>
                        </span>
                      )}
                    </span>
                  </Card.Title>
                )}
              </Card.Body>
            </Card>
          </Col>
        </Row>
      )}
      {statusData && (
        <Row>
          <Col>
            <LogDisplay statusData={statusData} />
          </Col>
        </Row>
      )}
    </Container>
  );
};

export default Protokoll;
