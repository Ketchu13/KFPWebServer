Imports System.Net.Sockets
Imports System.Net
Imports System.Threading
Imports System.Text
Imports System.Collections.Generic
Imports System.IO
Imports Microsoft.Win32


Public Class Server
    Public ServerIsRunning As Boolean = False
    Private serverSocket As Socket
    Private m_charEncoder As Encoding = Encoding.UTF8

    Private m_contentPath As String = ""
    Private m_TimeOut As Integer = 8

    Private m_Error404 As String = String.Concat("<html><head>", _
                                            "<meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">", _
                                            "</head><body style=""background-image:url(404.gif);", _
                                            "background-repeat:no-repeat; background-position:center center;", _
                                            "color: red;", _
                                            "background-color:black;""><h2>KFP ZBot Lite Simple Web Server</h2>", _
                                            "<!--div>404 - Not Found</div--></body></html>")
    '{ "extension", "content type" }
    Private m_Err501 As String = String.Concat("<html><head>", _
                                                "<meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">", _
                                                "</head><body><h2>KFP ZBot Lite Simple Web Server</h2>", _
                                                "<div>501 - Method Not Implemented</div></body></html>")

    Private m_extensions As New Dictionary(Of String, String)() From { _
         {"htm", "text/html"}, _
         {"html", "text/html"}, _
         {"xml", "text/xml"}, _
         {"txt", "text/plain"}, _
         {"css", "text/css"}, _
         {"png", "image/png"}, _
         {"gif", "image/gif"}, _
         {"jpg", "image/jpg"}, _
         {"jpeg", "image/jpeg"}, _
         {"zip", "application/zip"}, _
         {"py", "text/html"}, _
         {"kfp", "text/plain"}, _
         {"csv", "text/plain"}, _
         {"js", "application/javascript"}}
    Public Event NewEvent As NewEventEventHandler
    Public Delegate Sub NewEventEventHandler(ByVal httpMethod As String, ByVal value As String, ByVal Ip As String)
    Public Event ErrorEvent As ErrorEventEventHandler
    Public Delegate Sub ErrorEventEventHandler(ByVal httpMethod As String, ByVal value As String, ByVal Ip As String)

  
#Region "Properties"
    Public Property Err501() As String
        Get
            Return m_Err501
        End Get
        Set(ByVal value As String)
            If value.Length >= 1 Then
                m_Err501 = value
            End If
        End Set
    End Property
    Public Property TimeOut() As Integer
        Get
            Return m_TimeOut
        End Get
        Set(ByVal value As Integer)
            If value >= 1 Then
                m_TimeOut = value
            End If
        End Set
    End Property
    Public Property Err404() As String
        Get
            Return m_Error404
        End Get
        Set(ByVal value As String)
            If value.Length >= 1 Then
                m_Error404 = value
            End If
        End Set
    End Property
    Public Property Extensions() As Dictionary(Of String, String)
        Get
            Return m_extensions
        End Get
        Set(ByVal value As Dictionary(Of String, String))
            m_extensions = value
        End Set
    End Property
    Public Property CharEncoder() As Encoding
        Get
            Return m_CharEncoder
        End Get
        Set(ByVal value As Encoding)
            m_charEncoder = value
        End Set
    End Property
#End Region

    Public Function start(ByVal ipAddress As IPAddress, ByVal port As Integer, ByVal maxNOfCon As Integer, ByVal contentPath As String) As Boolean
        If ServerIsRunning Then
            Return False
        End If
        Try
            serverSocket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            serverSocket.Bind(New IPEndPoint(ipAddress, port))
            serverSocket.Listen(maxNOfCon)
            serverSocket.ReceiveTimeout = TimeOut
            serverSocket.SendTimeout = TimeOut
            ServerIsRunning = True
            Me.m_contentPath = contentPath
        Catch ex As System.Exception
            RaiseErrorEvent("none", "Error", ex.Message)
            Return False
        End Try
        Dim requestListenerT As New Thread(Sub()
                                               While ServerIsRunning
                                                   Dim clientSocket As Socket
                                                   Try
                                                       clientSocket = serverSocket.Accept()
                                                       Dim requestHandler As New Thread(Sub()
                                                                                            clientSocket.ReceiveTimeout = TimeOut
                                                                                            clientSocket.SendTimeout = TimeOut
                                                                                            Try
                                                                                                handleTheRequest(clientSocket)
                                                                                            Catch
                                                                                                Try
                                                                                                    clientSocket.Close()
                                                                                                Catch
                                                                                                End Try
                                                                                            End Try
                                                                                        End Sub)
                                                       requestHandler.Start()
                                                   Catch ex As System.Exception
                                                       RaiseErrorEvent("none", "Error", ex.Message)
                                                   End Try
                                               End While
                                           End Sub)
        requestListenerT.Start()
        Return True
    End Function

    Public Sub [stop]()
        If ServerIsRunning Then
            ServerIsRunning = False
            Try
                serverSocket.Close()
            Catch ex As System.Exception
                RaiseErrorEvent("none", "Error", ex.Message)
            End Try
            serverSocket = Nothing
        End If
    End Sub
    Public Sub RaiseNewEvent(ByVal httpMethod As String, ByVal value As String, ByVal Ip As String)
        RaiseEvent NewEvent(httpMethod, value, Ip)
    End Sub
    Public Sub RaiseErrorEvent(ByVal httpMethod As String, ByVal value As String, ByVal Ip As String)
        RaiseEvent ErrorEvent(httpMethod, value, Ip)
    End Sub
    Private Function GetTypeOfFile(ByVal registryKey As RegistryKey, ByVal fileName As String) As String
        Dim mimeType As String = "application/unknown"
        Dim fileClass As RegistryKey = registryKey.OpenSubKey(Path.GetExtension(fileName).ToLower())
        If Not fileClass Is Nothing AndAlso Not fileClass.GetValue("Content Type") Is Nothing Then
            mimeType = fileClass.GetValue("Content Type").ToString()
        End If
        Return mimeType
    End Function
    Private Sub handleTheRequest(ByVal clientSocket As Socket)
        Dim buffer As Byte() = New Byte(10239) {}
        Dim receivedBCount As Integer = clientSocket.Receive(buffer)
        Dim strReceived As String = charEncoder.GetString(buffer, 0, receivedBCount)
        Dim httpMethod As String = strReceived.Substring(0, strReceived.IndexOf(" "))
        Dim _params As String() = strReceived.Split(vbLf)
        Dim referer As String = ""
        Dim userAgent As String = ""
        ' Dim origin As String = ""
        'Dim host As String = ""
        ' Dim connection As String = ""

        Dim ipend As String = ""
        Dim strQuery As String = ""
        Try
            For i As Integer = 0 To _params.Length - 1
                If (i = _params.Length - 1) Then
                    Try
                        ' RaiseErrorEvent("StringQuery", strQuery, "str");
                        strQuery = _params(i)
                    Catch ex As System.Exception
                        RaiseErrorEvent("none", ex.Message, "Error")
                    End Try
                Else
                    Dim param As String() = _params(i).Split(":"c)
                    If param.Length >= 2 Then
                        If param(0).ToLower().Equals("referer") Then
                            referer = param(1)
                        End If
                        If param(0).ToLower().Equals("user-agent") Then
                            userAgent = param(1)
                        End If
                        '  If param(0).ToLower().Equals("origin") Then
                        'origin = param(1)
                        'End If
                        '  If param(0).ToLower().Equals("host") Then
                        'host = param(1)
                        ' End If

                        'If param(0).ToLower().Equals("connection") Then
                        'connection = param(1)
                        'RaiseErrorEvent(httpMethod, param[1], param[0]);
                        ' End If
                    End If
                End If
            Next
        Catch ex As System.Exception
            RaiseErrorEvent("none", ex.Message, "Error")
        End Try
        Dim start As Integer = strReceived.IndexOf(httpMethod) + httpMethod.Length + 1
        Dim length As Integer = strReceived.LastIndexOf("HTTP") - start - 1
        'RaiseErrorEvent(httpMethod,"header", strReceived);

        Dim requestedUrl As String = strReceived.Substring(start, length)
        ' RaiseErrorEvent(httpMethod, "requestedUrl", requestedUrl);
        '  Dim splited1 As String() = requestedUrl.Split("?"c)
        '  RaiseErrorEvent(httpMethod, "splited1.Length", splited1.Length.ToString());
        Dim requestedFile As String
        If httpMethod.Equals("GET") Then
            requestedFile = requestedUrl.Split("?"c)(0)
            ipend = clientSocket.RemoteEndPoint.ToString()
            '' GetCgiData(requestedFile,"","",ipend.Split(":")[0],,)

            RaiseNewEvent(httpMethod, requestedFile, ipend)
        ElseIf httpMethod.Equals("POST") Then
            requestedFile = requestedUrl.Split("?"c)(0)
            ipend = clientSocket.RemoteEndPoint.ToString()
            RaiseNewEvent(httpMethod, requestedFile, ipend)
            '' GetCgiData(requestedFile,"","",ipend.Split(":")[0],,)
            ' RaiseNewEvent(requestedFile, ipend);
            'Try
            '    ' && _params[i - 1].Equals("/n") == true

            '    Dim splited As String() = strQuery.Split("&"c)

            '    For i As Integer = 0 To splited.Length - 1
            '        If splited(i).ToString().Contains("m_Error404") = True Then
            '            Dim phz As String = splited(i).ToString()
            '            Dim param1 As String = phz.Substring(0, phz.IndexOf("="))
            '            ' RaiseNewEvent(httpMethod, param1Value, param1);
            '            Dim param1Value As String = phz.Substring(phz.IndexOf("=") + 1)
            '            ' RaiseErrorEvent(httpMethod, splited[i].ToString(), ipend);
            '        Else
            '        End If
            '    Next
            'Catch ex As System.Exception
            '    RaiseErrorEvent(httpMethod, "Error", ex.Message)
            'End Try
        Else
            notImplemented(clientSocket)
            Return
        End If
        requestedFile = requestedFile.Replace("/", "\").Replace("\..", "")
        start = requestedFile.LastIndexOf("."c) + 1
        If start > 0 Then
            length = requestedFile.Length - start
            Dim extension As String = requestedFile.Substring(start, length)
            If extensions.ContainsKey(extension) Then
                If File.Exists(m_contentPath & requestedFile) Then
                    Dim cgi As String = GetCgiData(requestedFile, strQuery, extension, ipend, "HTTP/1.1", referer, _
                     httpMethod, userAgent, strQuery)
                    Dim extRK As String = GetTypeOfFile(Registry.ClassesRoot, (m_contentPath & requestedFile))
                    If cgi.Length > 2 Then
                        sendOkResponse(clientSocket, System.Text.Encoding.UTF8.GetBytes(cgi), extensions(extension))
                    Else
                        sendOkResponse(clientSocket, File.ReadAllBytes(m_contentPath & requestedFile), extRK)
                    End If
                Else
                    notFound(clientSocket)
                End If
            End If
        Else
            If requestedFile.Substring(length - 1, 1) <> "\" Then
                requestedFile += "\"
            End If
            If File.Exists(m_contentPath & requestedFile & "index.htm") Then
                sendOkResponse(clientSocket, File.ReadAllBytes(m_contentPath & requestedFile & "\index.htm"), "text/html")
            ElseIf File.Exists(m_contentPath & requestedFile & "index.html") Then
                sendOkResponse(clientSocket, File.ReadAllBytes(m_contentPath & requestedFile & "\index.html"), "text/html")
            ElseIf File.Exists(m_contentPath & requestedFile & "index.kfp") Then
                sendOkResponse(clientSocket, File.ReadAllBytes(m_contentPath & requestedFile & "\index.kfp"), "text/html")
            Else
                notFound(clientSocket)
            End If
        End If
    End Sub

    Private Sub notImplemented(ByVal clientSocket As Socket)
        sendResponse(clientSocket, m_Err501, "501 Not Implemented", "text/html")
    End Sub

    Private Sub notFound(ByVal clientSocket As Socket)
        sendResponse(clientSocket, m_Error404, "404 Not Found", "text/html")
    End Sub

    Private Sub sendOkResponse(ByVal clientSocket As Socket, ByVal bContent As Byte(), ByVal contentType As String)
        sendResponse(clientSocket, bContent, "200 OK", contentType)
    End Sub

    Private Sub sendResponse(ByVal clientSocket As Socket, ByVal strContent As String, ByVal responseCode As String, ByVal contentType As String)
        Dim bContent As Byte() = charEncoder.GetBytes(strContent)
        sendResponse(clientSocket, bContent, responseCode, contentType)
    End Sub

    Private Sub sendResponse(ByVal clientSocket As Socket, ByVal bContent As Byte(), ByVal responseCode As String, ByVal contentType As String)
        Try
            Dim bHeader As Byte() = charEncoder.GetBytes("HTTP/1.1 " & responseCode & vbCr & vbLf & "Server: KFP ZBot Lite Simple CGI Web Server" & vbCr & vbLf & "Content-Length: " & bContent.Length.ToString() & vbCr & vbLf & "Connection: close" & vbCr & vbLf & "Content-Type: " & contentType & vbCr & vbLf & vbCr & vbLf)
            clientSocket.Send(bHeader)
            clientSocket.Send(bContent)
            clientSocket.Close()
        Catch
        End Try
    End Sub

    '******
    Private Function GetCgiData(ByVal cgiFile As String, _
                                ByVal QUERY_STRING As String, _
                                ByVal ext As String, _
                                ByVal remote_address As String, _
                                ByVal SERVER_PROTOCOL As String, _
                                ByVal REFERER As String, _
                                ByVal REQUESTED_METHOD As String, _
                                ByVal USER_AGENT As String, _
                                ByVal request As String) As String

        Dim proc As New Process()
        Dim str As String = Nothing
        'RaiseErrorEvent("cgi ext", ext, "");
        If ext = "php" Then
            proc.StartInfo.FileName = "e" & "\\php-cgi.exe"
            If Not File.Exists(proc.StartInfo.FileName) Then
                Return "File not found !"
            End If
            proc.StartInfo.Arguments = " -q " & cgiFile & " " & QUERY_STRING
            Dim script_name As String = cgiFile.Substring(cgiFile.LastIndexOf("\"c) + 1)
            RaiseErrorEvent("cgi script_name", script_name, "")
            proc.StartInfo.EnvironmentVariables.Add("REMOTE_ADDR", remote_address.ToString())
            proc.StartInfo.EnvironmentVariables.Add("SCRIPT_NAME", script_name)
            proc.StartInfo.EnvironmentVariables.Add("USER_AGENT", USER_AGENT)
            proc.StartInfo.EnvironmentVariables.Add("REQUESTED_METHOD", REQUESTED_METHOD)
            proc.StartInfo.EnvironmentVariables.Add("REFERER", REFERER)
            proc.StartInfo.EnvironmentVariables.Add("SERVER_PROTOCOL", SERVER_PROTOCOL)
            proc.StartInfo.EnvironmentVariables.Add("QUERY_STRING", request)
        Else
            If ext = "py" Then
                proc.StartInfo.FileName = "python.exe"
                ' RaiseErrorEvent("cgi StartInfo cgiFile", m_contentPath + cgiFile, "z" + ext + "z");
                proc.StartInfo.Arguments = m_contentPath & cgiFile & " " & QUERY_STRING
            Else
                Return String.Empty
            End If
        End If
        proc.StartInfo.UseShellExecute = False
        proc.StartInfo.RedirectStandardOutput = True
        proc.StartInfo.RedirectStandardInput = True
        proc.StartInfo.WorkingDirectory = m_contentPath
        ' proc.StartInfo.CreateNoWindow = true;
        str = ""
        proc.Start()
        str = proc.StandardOutput.ReadToEnd()
        proc.Close()
        proc.Dispose()
        Return str
    End Function
End Class
