Imports System.Net
Imports System.Net.Sockets
Imports System.Threading


Public Class NetworkTransaction

    Const BufferSize As Integer = 5120
    Const PollInterval As Integer = 100

    Dim _socket As Socket
    Dim _strm As NetworkStream
    Dim _delimiter As String = ControlChars.CrLf
    Dim _bConnected As Boolean = False

    Dim _baDelimiter() As Byte

    Dim _strRecievedLine As String = String.Empty
    Dim _eResponseRecieved As New AutoResetEvent(False)

    Dim tListener As Thread

    Public Sub New()
        _socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

        _baDelimiter = System.Text.ASCIIEncoding.ASCII.GetBytes(Delimiter)
    End Sub

    Public Function Connect(ByVal Host As String, ByVal Port As Integer) As Boolean
        Dim bConnected As Boolean = False

        _socket.Connect(Host, Port)

        If _socket.Connected Then
            _strm = New NetworkStream(_socket)
            _bConnected = True

            tListener = New Thread(AddressOf Listener)
            tListener.Start()

            bConnected = True
        End If

        Return bConnected

    End Function

    Public Sub Disconnect()
        Try
            _bConnected = False
            tListener.Join()
            If Not _strm Is Nothing Then _strm.Close()
            If _socket.Connected Then _socket.Disconnect(True)
        Catch ex As Exception
            If Not _strm Is Nothing Then _strm.Close()
            If _socket.Connected Then _socket.Disconnect(True)
        End Try
        

    End Sub

    Public Property Delimiter() As String
        Get
            Return _delimiter
        End Get
        Set(ByVal Value As String)
            _delimiter = Value
            _baDelimiter = System.Text.ASCIIEncoding.ASCII.GetBytes(Delimiter)
        End Set
    End Property

    Public Sub Listener()

        Dim iBytesRead As Integer
        Dim bDelimiterFound As Boolean = False
        Dim i, j As Integer
        Dim _iInputBufferPos As Integer
        Dim _baInputBuffer(BufferSize) As Byte

        While _bConnected = True
            Try

                iBytesRead = _strm.Read(_baInputBuffer, _iInputBufferPos, BufferSize - _iInputBufferPos)

                If iBytesRead > 0 Then
                    ' Scan through the buffer for the delimiter
                    bDelimiterFound = False
                    i = _iInputBufferPos
                    While i < _iInputBufferPos + iBytesRead - (_baDelimiter.Length - 1) AndAlso Not bDelimiterFound
                        bDelimiterFound = True

                        j = 0
                        While bDelimiterFound AndAlso j < _baDelimiter.Length
                            If _baInputBuffer(i + j) <> _baDelimiter(j) Then
                                bDelimiterFound = False
                            Else
                                j += 1
                            End If
                        End While
                        If Not bDelimiterFound Then i += 1
                    End While

                    _iInputBufferPos += iBytesRead

                    If bDelimiterFound Then

                        ' Create a string with the text up to the delimiter
                        Dim strLine As String = System.Text.ASCIIEncoding.ASCII.GetString(_baInputBuffer, 0, i)

                        ' Create a new Byte array, and copy everything else in the buffer to that...
                        Dim baNewBuffer(BufferSize) As Byte

                        _baInputBuffer = baNewBuffer
                        _iInputBufferPos = 0

                        '
                        SyncLock _strRecievedLine
                            _strRecievedLine = strLine
                        End SyncLock
                        _eResponseRecieved.Set()
                    End If

                End If

            Catch ex As Exception

            End Try

            Thread.Sleep(PollInterval)

        End While

    End Sub

    Public Function Transact(ByVal toSend As String, Optional ByVal TimeOut As Integer = 1000) As String

        Dim baOutputBuffer() As Byte = System.Text.ASCIIEncoding.ASCII.GetBytes(toSend + _delimiter)
        Dim strLine As String = String.Empty

        _eResponseRecieved.Reset()

        ' Send the data
        _strm.Write(baOutputBuffer, 0, baOutputBuffer.Length)

        ' Wait for a response
        If _eResponseRecieved.WaitOne(TimeOut, False) Then
            SyncLock _strRecievedLine
                strLine = _strRecievedLine
            End SyncLock
        End If

        Return strLine

    End Function

    Public Function ReadLine(Optional ByVal TimeOut As Integer = 1000)
        Dim strLine As String = String.Empty
        If _eResponseRecieved.WaitOne(TimeOut, False) Then
            SyncLock _strRecievedLine
                strLine = _strRecievedLine
            End SyncLock
        End If

        Return strLine
    End Function

End Class
