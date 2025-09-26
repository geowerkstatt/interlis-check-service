# Changelog

## [Unreleased]

## v5.0.185 - 2025-09-26

### Changed

- Added local INTERLIS model repository for configuration purposes. Removed legacy configuration support for catalogues, configs and custom model directories.
- Updated various 3rd party libraries.

### Fixed

- Fixed interchanged UID and GID in docker entrypoint script.

## v4.1.156 - 2025-06-10

### Changed

- Updated various 3rd party libraries.

## v4.1.151 - 2025-04-28

### Changed

- Redesign of the user interface.

## v4.0.145 - 2025-04-03

### Changed

- The code for this application is now available under the AGPL 3.0 licence.
- The image now supports to use existing ilivalidator and ili2gpkg executables in `$ILITOOLS_HOME_DIR` directory.
- The image now supports overriding the ilitools cache directory via `ILITOOLS_CACHE_DIR` env variable.

### Fixed

- Fixed release workflow to tag and publish major version tags.

## v4.0.115 - 2024-09-02

### Changed

- Updated to .NET 8.0.
- The app now runs on port 8080 inside the docker container. Please update your Docker compose!
- Default uid & gid of container user changed to 1654.

## v3.0.110 - 2024-05-16

### Added

- Visualize log entries in a tree view.
- Log entries with coordinates using the LV95 Reference System can now be downloaded as GeoJSON.
- Added `--verbose` flag when calling _ilivalidator_ to get a more detailed output about constraints and errors.
- When releasing a GitHub pre-release, the release notes are automatically updated with the corresponding entries from the `CHANGELOG.md` file.

### Changed

- Switch to the built-in `Parallel.ForEachAsync` method for parallel processing of validation background tasks.

### Fixed

- Fixed the HTTP Content-Type header for the _ilivalidator_ plain text log files.
- Fixed transfer files not getting deleted after failed validations.

## v3.0.98 (2024-03-18)

### Added

- Added support for additional catalogue files pre-configured in the backend. Which means that the user does not have to upload required catalogue files along with the transfer file in a ZIP file for every validation anymore. The backend will automatically use the pre-configured catalogue files for validation if the user does not provide them in a ZIP file.

## v3.0.85 (2023-01-31)

### Added

- Added support for additional ilivalidator plugins.
- Added support for local INTERLIS models.

## v3.0.74 (2022-09-19)

### Added

- Support REST API
- Communication of HTML-Client switched to RESTful (before: SignalR)
- Support validation against local catalogue files
- Support validation against catalogue files delivered by ZIP

## v2.1.69 (2022-05-11)

### Added

- Support INTERLIS 1 transfer files
- Support validation against own models (ili) delivered by ZIP
- Version- and License information included at client

## v2.1.56 (2022-05-11)

### Fixed

- Fixed TOML configuration option

## v2.1.54 (2022-02-28)

### Added

- Support of validation of Geopackage databases (containing an ili2db schema)

## v2.0.46 (2022-02-09)

### Fixed

- Fixed an issue where network disconnects stops validations
- Fixed an issue with transferfiles containing a blank space

### Added

- Added a new UI design
- Enhanced support of frontend customization
- Improved error logs in the case of unavailable logfiles
- Improved status logs and display

## v0.10.42 (2021-11-09)

### Added

- Support for automatic deletes of logfiles

### Fixed

- Improved scrollbar position
- Fixed status in the case of a connection timeout
- Fixed an issue with deletes in the case of an invalid upload
- Fixed some CSS issues

## v0.9.29 (2021-09-08)

### Added

- Improved filename specification for logfiles
- Support for automatically deletes of transferfiles after validations
- Added option to provide path to xtf-logfile to clipboard
- Added controller logs

### Changed

- Improved wrong content-type/encoding of logfiles

### Fixed

- Fixed an issue with too large request bodies
- Fixed an issue with upper case extensions of transferfiles

## v0.9.12 (2021-08-18)

### Added

- Support for download of log- and xtflog-files from client
- Enhanced parameter set for calling ilivalidator
- Add support to run ilicop in a Docker container
- Added version to frontend

### Fixed

- Fixed an issue while uploading ZIP files
