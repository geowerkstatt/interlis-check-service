import "./app.css";
import { useState, useEffect, useRef, useCallback } from "react";
import { Container } from "react-bootstrap";
import { FileDropzone } from "./dropzone";
import { Title } from "./title";
import Protokoll from "./protokoll";
import { UploadForm } from "./uploadForm";

export const Home = (props) => {
  const {
    clientSettings,
    nutzungsbestimmungenAvailable,
    showNutzungsbestimmungen,
    quickStartContent,
    setShowBannerContent,
  } = props;
  const [fileToCheck, setFileToCheck] = useState(null);
  const fileToCheckRef = useRef(fileToCheck);
  const [validationRunning, setValidationRunning] = useState(false);
  const [statusInterval, setStatusInterval] = useState(null);
  const [statusData, setStatusData] = useState(null);
  const [customAppLogoPresent, setCustomAppLogoPresent] = useState(false);
  const [checkedNutzungsbestimmungen, setCheckedNutzungsbestimmungen] = useState(false);
  const [isFirstValidation, setIsFirstValidation] = useState(true);
  const [log, setLog] = useState([]);
  const [uploadLogsInterval, setUploadLogsInterval] = useState(0);
  const [uploadLogsEnabled, setUploadLogsEnabled] = useState(false);
  const [selectedProfile, setSelectedProfile] = useState(null);

  // Enable Upload logging
  useEffect(() => uploadLogsInterval && setUploadLogsEnabled(true), [uploadLogsInterval]);
  useEffect(() => !uploadLogsEnabled && clearInterval(uploadLogsInterval), [uploadLogsEnabled, uploadLogsInterval]);

  const resetLog = useCallback(() => setLog([]), [setLog]);
  const updateLog = useCallback(
    (message, { disableUploadLogs = true } = {}) => {
      if (disableUploadLogs) setUploadLogsEnabled(false);
      setLog((log) => {
        if (message === log[log.length - 1]) return log;
        else return [...log, message];
      });
    },
    [setUploadLogsEnabled]
  );

  // Reset log and abort upload on file change
  useEffect(() => {
    resetLog();
    setStatusData(null);
    setValidationRunning(false);
    setUploadLogsEnabled(false);
    if (statusInterval) clearInterval(statusInterval);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fileToCheck]);

  // Show banner on first validation
  useEffect(() => {
    if (validationRunning && isFirstValidation) {
      setTimeout(() => {
        setShowBannerContent(true);
        setIsFirstValidation(false);
      }, 2000);
    }
  }, [validationRunning, isFirstValidation, setShowBannerContent, setIsFirstValidation]);

  const logUploadLogMessages = () => updateLog(`${fileToCheck.name} hochladen...`, { disableUploadLogs: false });
  const setIntervalImmediately = (func, interval) => {
    func();
    return setInterval(func, interval);
  };
  const checkFile = (e) => {
    e.stopPropagation();
    resetLog();
    setStatusData(null);
    setValidationRunning(true);
    setUploadLogsInterval(setIntervalImmediately(logUploadLogMessages, 2000));
    uploadFile(fileToCheck);
  };

  const uploadFile = async (file) => {
    const formData = new FormData();
    formData.append("file", file, file.name);
    if (selectedProfile) {
      formData.append("profile", selectedProfile);
    }
    const response = await fetch(`api/v1/upload`, {
      method: "POST",
      body: formData,
    });
    if (response.ok) {
      // Use ref instead of state to check current file status in async function
      if (fileToCheckRef.current) {
        const data = await response.json();
        const getStatusData = async (data) => {
          const status = await fetch(data.statusUrl, {
            method: "GET",
          });
          const statusData = await status.json();
          return statusData;
        };

        const interval = setIntervalImmediately(async () => {
          const statusData = await getStatusData(data);
          updateLog(statusData.statusMessage);
          if (
            statusData.status === "completed" ||
            statusData.status === "completedWithErrors" ||
            statusData.status === "failed"
          ) {
            clearInterval(interval);
            setValidationRunning(false);
            setStatusData(statusData);
          }
        }, 2000);
        setStatusInterval(interval);
      }
    } else {
      console.log("Error while uploading file: " + response.json());
      updateLog("Der Upload war nicht erfolgreich. Die Validierung wurde abgebrochen.");
      setValidationRunning(false);
    }
  };

  const resetForm = useCallback(() => {
    setFileToCheck(null);
    setValidationRunning(false);
    setCheckedNutzungsbestimmungen(false);
    setSelectedProfile(null);
    setUploadLogsEnabled(false);
    if (uploadLogsInterval) clearInterval(uploadLogsInterval);
  }, [setFileToCheck, setValidationRunning, setUploadLogsEnabled, uploadLogsInterval]);

  return (
    <main>
      <Container className="main-container">
        <Title
          clientSettings={clientSettings}
          customAppLogoPresent={customAppLogoPresent}
          setCustomAppLogoPresent={setCustomAppLogoPresent}
          quickStartContent={quickStartContent}
        />
      </Container>
      <Container>
        <FileDropzone
          acceptedFileTypes={clientSettings?.acceptedFileTypes}
          fileToCheck={fileToCheck}
          fileToCheckRef={fileToCheckRef}
          setFileToCheck={setFileToCheck}
          validationRunning={validationRunning}
          resetForm={resetForm}
        />
        {fileToCheck && (
          <UploadForm
            nutzungsbestimmungenAvailable={nutzungsbestimmungenAvailable}
            checkedNutzungsbestimmungen={checkedNutzungsbestimmungen}
            showNutzungsbestimmungen={showNutzungsbestimmungen}
            setCheckedNutzungsbestimmungen={setCheckedNutzungsbestimmungen}
            validationRunning={validationRunning}
            setValidationRunning={setValidationRunning}
            startValidation={checkFile}
            resetForm={resetForm}
            selectedProfile={selectedProfile}
            setSelectedProfile={setSelectedProfile}
          />
        )}
      </Container>
      <Protokoll
        log={log}
        statusData={statusData}
        fileName={fileToCheck ? fileToCheck.name : ""}
        validationRunning={validationRunning}
      />
    </main>
  );
};

export default Home;
