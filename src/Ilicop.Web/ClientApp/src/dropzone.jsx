import { useCallback, useState } from "react";
import { useDropzone } from "react-dropzone";
import { MdCancel, MdFileUpload } from "react-icons/md";
import styled from "styled-components";
import { Spinner } from "react-bootstrap";

const getColor = (props) => {
  if (props.isDragActive) {
    return "#124A4F";
  } else {
    return "#124A4F99";
  }
};

const Container = styled.div`
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  min-height: 15vh;
  max-width: 95vw;
  font-size: 0.78125rem;
  border-width: 2px;
  border-radius: 5px;
  border-color: ${(props) => getColor(props)};
  border-style: dashed;
  background-color: #124a4f0d;
  outline: none;
  transition: border 0.24s ease-in-out;
`;

export const FileDropzone = ({
  acceptedFileTypes,
  fileToCheck,
  fileToCheckRef,
  setFileToCheck,
  validationRunning,
  resetForm,
}) => {
  const [dropZoneError, setDropZoneError] = useState(undefined);

  const onDropAccepted = useCallback(
    (acceptedFiles) => {
      // dropZone max file is defined as 1;
      setFileToCheck(acceptedFiles[0]);
      fileToCheckRef.current = acceptedFiles[0];
      setDropZoneError(undefined);
    },
    [fileToCheckRef, setFileToCheck]
  );

  const onDropRejected = useCallback(
    (fileRejections) => {
      const errorCode = fileRejections[0].errors[0].code;
      switch (errorCode) {
        case "file-invalid-type":
          setDropZoneError(
            `Der Dateityp wird nicht unterstützt. Bitte wähle eine Datei (max. 200MB) mit einer der folgenden Dateiendungen: ${acceptedFileTypes}`
          );
          break;
        case "too-many-files":
          setDropZoneError("Es kann nur eine Datei aufs Mal geprüft werden.");
          break;
        case "file-too-large":
          setDropZoneError(
            "Die ausgewählte Datei ist über 200MB gross. Bitte wähle eine kleinere Datei oder erstelle eine ZIP-Datei."
          );
          break;
        default:
          setDropZoneError(
            `Bitte wähle eine Datei (max. 200MB) mit einer der folgenden Dateiendungen: ${acceptedFileTypes}`
          );
      }
      resetForm();
    },
    [resetForm, acceptedFileTypes]
  );

  const removeFile = (e) => {
    e.stopPropagation();
    resetForm();
  };

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDropAccepted,
    onDropRejected,
    maxFiles: 1,
    maxSize: 209715200,
    accept: acceptedFileTypes,
  });

  return (
    <div className="dropzone-wrapper">
      <Container {...getRootProps({ isDragActive })}>
        <input {...getInputProps()} />
        {!(fileToCheck || dropZoneError) && (
          <div className="dropzone dropzone-text-disabled">
            Datei {acceptedFileTypes} hier ablegen oder klicken um vom lokalen Dateisystem auszuwählen.
            <p className="drop-icon">
              <MdFileUpload />
            </p>
          </div>
        )}
        {fileToCheck && (
          <div className="dropzone dropzone-text-file">
            <span onClick={removeFile}>
              <MdCancel className="dropzone-icon" />
            </span>
            {fileToCheck.name}
            {validationRunning && (
              <div>
                <Spinner className="spinner" animation="border" />
              </div>
            )}
          </div>
        )}
        {dropZoneError && (
          <div className="dropzone dropzone-text-error">
            {dropZoneError}
            <p className="drop-icon">
              <MdFileUpload />
            </p>
          </div>
        )}
      </Container>
    </div>
  );
};
