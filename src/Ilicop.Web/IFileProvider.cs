using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Geowerkstatt.Ilicop.Web
{
    /// <summary>
    /// Provides read/write access to files in a predefined folder.
    /// </summary>
    public interface IFileProvider
    {
        /// <summary>
        /// Gets the home directory.
        /// </summary>
        DirectoryInfo HomeDirectory { get; }

        /// <summary>
        /// Creates or overwrites the specified <paramref name="file"/>.
        /// </summary>
        /// <param name="file">The name of the file to create.</param>
        /// <returns>A <see cref="FileStream"/> that provides read/write access to the file specified.</returns>
        FileStream CreateFile(string file);

        /// <summary>
        /// Opens an existing UTF-8 encoded text file for reading.
        /// </summary>
        /// <param name="file">The file to be opened for reading.</param>
        /// <returns>A <see cref="StreamReader"/> on the specified file.</returns>
        /// <exception cref="InvalidOperationException">If the file provider is not yet initialized.</exception>
        StreamReader OpenText(string file);

        /// <summary>
        /// Determines whether the specified <paramref name="file"/> exists.
        /// </summary>
        /// <param name="file">The file to check.</param>
        /// <returns><c>true</c> if the caller has the required permissions and path contains the name of
        /// an existing file; otherwise, <c>false</c>.</returns>
        bool Exists(string file);

        /// <summary>
        /// Enumerates the current <see cref="HomeDirectory"/>.
        /// </summary>
        /// <returns>Returns the contents of the <see cref="HomeDirectory"/>.</returns>
        /// <exception cref="InvalidOperationException">If the file provider is not yet initialized.</exception>
        IEnumerable<string> GetFiles();

        /// <summary>
        /// Asynchronously deletes the <paramref name="file"/> specified.
        /// </summary>
        /// <param name="file">The name of the file to be deleted.</param>
        /// <exception cref="InvalidOperationException">If the file provider is not yet initialized.</exception>
        Task DeleteFileAsync(string file);

        /// <summary>
        /// Initializes this file provider. Creates and sets the <see cref="HomeDirectory"/>
        /// to the folder with the <paramref name="id"/> specified.
        /// </summary>
        /// <param name="id">The specified folder id to be created.</param>
        void Initialize(Guid id);
    }
}
