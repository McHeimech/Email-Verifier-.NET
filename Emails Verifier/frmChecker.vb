Imports System.Net
Imports System.Data.SqlClient

Public Class frmChecker

#Region "Private members"

    Dim LocalConn = "Data Source={0};Initial Catalog={1};Integrated Security=True"
    Dim RemoteConn = "Data Source={0};Initial Catalog={1};Persist Security Info=True;User ID={2};Password={3}"

#End Region

#Region "Properties"

    Private _ConnectionString As String
    Public Property ConnectionString() As String
        Get
            Return _ConnectionString
        End Get
        Set(ByVal value As String)
            _ConnectionString = value
        End Set
    End Property

    Private _ServerIP As IPAddress
    Public Property ServerIP() As IPAddress
        Get
            Return _ServerIP
        End Get
        Set(ByVal value As IPAddress)
            _ServerIP = value
        End Set
    End Property

    Private _Database As String
    Public Property Database() As String
        Get
            Return _Database
        End Get
        Set(ByVal value As String)
            _Database = value
        End Set
    End Property

    Private _Table As String
    Public Property Table() As String
        Get
            Return _Table
        End Get
        Set(ByVal value As String)
            _Table = value
        End Set
    End Property

    Private _EmailColumn As String
    Public Property EmailColumn() As String
        Get
            Return _EmailColumn
        End Get
        Set(ByVal value As String)
            _EmailColumn = value
        End Set
    End Property

#End Region

#Region "Events"

#Region "Form load"

    Private Sub frmChecker_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Log("Ready...")
    End Sub

#End Region

#Region "Connect Button"

    Private Sub btnConnect_Click(sender As Object, e As EventArgs) Handles btnConnect.Click
        Log("Checking IP address...")
        IPAddress.TryParse(txtServer.Text.Trim(), ServerIP)

        If ServerIP Is Nothing Then
            Log("Invalid IP address")
            MessageBox.Show("Invalid Server IP!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Exit Sub
        End If

        Log("IP address is valid")

        If chkWindowsAuth.Checked Then
            ConnectionString = String.Format(LocalConn, ServerIP.ToString(), "Master")
        Else
            ConnectionString = String.Format(RemoteConn, ServerIP.ToString(), "Master", _
                                             txtUsername.Text.Trim(), txtPassword.Text.Trim())
        End If

        Log("Connecting to SQL server...")
        Try
            cbDatabases.Items.Clear()
            Using conn As New SqlConnection(ConnectionString)
                Using cmd As New SqlCommand("Select * From sys.databases Where database_id > 4 Order by name", conn)
                    conn.Open()
                    Using Reader As SqlDataReader = cmd.ExecuteReader(CommandBehavior.CloseConnection)
                        While Reader.Read
                            cbDatabases.Items.Add(Reader("Name"))
                        End While
                    End Using
                End Using
            End Using
            cbDatabases.Items.Insert(0, "-- Select Database -- ")
            cbDatabases.SelectedIndex = 0
            cbDatabases.Enabled = True
            Log("Connection to SQL server was successfull")
        Catch ex As Exception
            Log("Unable to connect to SQL server")
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

#End Region

#Region "Authentication Checkbox"

    Private Sub chkWindowsAuth_CheckedChanged(sender As Object, e As EventArgs) Handles chkWindowsAuth.CheckedChanged
        txtUsername.Enabled = Not chkWindowsAuth.Checked
        txtPassword.Enabled = Not chkWindowsAuth.Checked
    End Sub

#End Region

#Region "Database change"

    Private Sub cbDatabases_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbDatabases.SelectedIndexChanged
        If IsNumeric(cbDatabases.SelectedIndex) Then
            If cbDatabases.SelectedIndex = 0 Then
                cbTables.Enabled = False
                cbColumns.Enabled = False
                btnGetData.Enabled = False
                btnCheck.Enabled = False
                Exit Sub
            End If

            Database = cbDatabases.SelectedItem

            If chkWindowsAuth.Checked Then
                ConnectionString = String.Format(LocalConn, ServerIP.ToString(), Database)
            Else
                ConnectionString = String.Format(RemoteConn, ServerIP.ToString(), Database, _
                                                 txtUsername.Text.Trim(), txtPassword.Text.Trim())
            End If

            Try
                cbTables.Items.Clear()
                Using conn As New SqlConnection(ConnectionString)
                    Using cmd As New SqlCommand("Select * from INFORMATION_SCHEMA.TABLES Where TABLE_TYPE='BASE TABLE' Order by TABLE_NAME", conn)
                        conn.Open()
                        Using Reader As SqlDataReader = cmd.ExecuteReader(CommandBehavior.CloseConnection)
                            While Reader.Read
                                cbTables.Items.Add(Reader("TABLE_NAME"))
                            End While
                        End Using
                    End Using
                End Using
                cbTables.Items.Insert(0, "-- Select Table -- ")
                cbTables.SelectedIndex = 0
                cbTables.Enabled = True
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

#End Region

#Region "Table change"

    Private Sub cbTables_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbTables.SelectedIndexChanged
        If IsNumeric(cbTables.SelectedIndex) Then
            If cbTables.SelectedIndex = 0 Then
                cbColumns.Enabled = False
                btnGetData.Enabled = False
                btnCheck.Enabled = False
                Exit Sub
            End If

            Table = cbTables.SelectedItem

            Try
                cbColumns.Items.Clear()
                Using conn As New SqlConnection(ConnectionString)
                    Using cmd As New SqlCommand("select * from INFORMATION_SCHEMA .COLUMNS Where TABLE_NAME=@TableName Order by COLUMN_NAME", conn)
                        cmd.Parameters.AddWithValue("@TableName", Table)

                        conn.Open()
                        Using Reader As SqlDataReader = cmd.ExecuteReader(CommandBehavior.CloseConnection)
                            While Reader.Read
                                cbColumns.Items.Add(Reader("COLUMN_NAME"))
                            End While
                        End Using
                    End Using
                End Using
                cbColumns.Items.Insert(0, "-- Select Table -- ")
                cbColumns.SelectedIndex = 0
                cbColumns.Enabled = True
                btnGetData.Enabled = True
            Catch ex As Exception
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End If
    End Sub

#End Region

#Region "Column change"

    Private Sub cbColumns_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbColumns.SelectedIndexChanged
        If IsNumeric(cbTables.SelectedIndex) Then
            If cbColumns.SelectedIndex = 0 Then
                btnCheck.Enabled = False
                Exit Sub
            End If

            EmailColumn = cbColumns.SelectedItem

            btnCheck.Enabled = True
        End If
    End Sub

#End Region

#Region "Get Data Button"

    Private Sub btnGetData_Click(sender As Object, e As EventArgs) Handles btnGetData.Click
        Log("Inporting table data...")
        GetTableData()
    End Sub

#End Region

#Region "Check Data Button"

    Private Sub btnCheck_Click(sender As Object, e As EventArgs) Handles btnCheck.Click
        Log("Inporting table data...")
        If GetTableData() Then
            btnGetData.Enabled = False
            btnCheck.Enabled = False
            Log("Starting checking process...")
            bwCheckEmail.RunWorkerAsync()
        End If
    End Sub

#End Region

#Region "BackgroundWorker DoWork"
    Private Sub bwCheckEmail_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles bwCheckEmail.DoWork
        Dim PctDone As Double = 0
        Dim RowsDone As Integer = 0

        For Each Row As DataGridViewRow In dgvData.Rows
            Dim objResponse As New CheckResponse()
            objResponse.RowIndex = Row.Index

            Try
                objResponse.IsValid = VerifyEmail.VerifyAddress(Row.Cells(EmailColumn).Value)
            Catch ex As Exception
                objResponse.Response = ex.Message
                objResponse.IsValid = False
            End Try

            RowsDone += 1
            PctDone = (RowsDone / dgvData.Rows.Count) * 100
            bwCheckEmail.ReportProgress(PctDone, objResponse)
        Next
    End Sub

#End Region

#Region "BackgroundWorker Progress changed"

    Private Sub bwCheckEmail_ProgressChanged(sender As Object, e As System.ComponentModel.ProgressChangedEventArgs) Handles bwCheckEmail.ProgressChanged
        Dim objResponse As CheckResponse = DirectCast(e.UserState, CheckResponse)

        Log("Checked row " & objResponse.RowIndex)

        If objResponse.Response Is Nothing Then
            dgvData.Rows(objResponse.RowIndex).Cells("ValidEmail").Value = objResponse.IsValid
            If objResponse.IsValid Then
                dgvData.Rows(objResponse.RowIndex).DefaultCellStyle.BackColor = Color.Green
            Else
                dgvData.Rows(objResponse.RowIndex).DefaultCellStyle.BackColor = Color.Red
            End If
        Else
            Log("Error checking row " & objResponse.RowIndex & vbCrLf & objResponse.Response)
        End If

        pbChecker.Value = e.ProgressPercentage
    End Sub

#End Region

#Region "BackgroundWorker Completed"

    Private Sub bwCheckEmail_RunWorkerCompleted(sender As Object, e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles bwCheckEmail.RunWorkerCompleted
        btnGetData.Enabled = True
        btnCheck.Enabled = True
    End Sub

#End Region

#Region "Single email validation button"

    Private Sub btnValidate_Click(sender As Object, e As EventArgs) Handles btnValidate.Click
        lblResult.ForeColor = Color.Black

        Dim EmailAddress As String = txtEmail.Text.Trim()
        If EmailAddress = String.Empty Then
            Log("Email address is empty")
            lblResult.Text = "Email address cannot be empty!"
            Exit Sub
        End If

        Log("Checking email address")
        lblResult.Text = "Validating address, please wait..."

        If VerifyEmail.VerifyAddress(txtEmail.Text.Trim()) Then
            lblResult.Text = String.Format("{0} is a valid email address", EmailAddress)
            lblResult.ForeColor = Color.Green
        Else
            lblResult.Text = String.Format("{0} is NOT a valid email address", EmailAddress)
            lblResult.ForeColor = Color.Red
        End If

        Log("Email address successfully checked")
    End Sub

#End Region

#End Region

#Region "Private functions"

#Region "Get table data"

    Private Function GetTableData() As Boolean
        Try
            dgvData.DataSource = Nothing
            dgvData.Rows.Clear()
            dgvData.Columns.Clear()
            Using conn As New SqlConnection(ConnectionString)
                Using cmd As New SqlCommand(String.Format("select * from {0} Order by 1 asc", _
                                                          Table), conn)

                    conn.Open()
                    Using Reader As SqlDataReader = cmd.ExecuteReader(CommandBehavior.CloseConnection)
                        Dim dt = New DataTable()
                        dt.Load(Reader)
                        dgvData.AutoGenerateColumns = True
                        dgvData.DataSource = dt
                        dgvData.Refresh()
                    End Using
                End Using
            End Using

            Dim ValidColumn As New DataGridViewCheckBoxColumn
            ValidColumn.HeaderText = "Email Valid"
            ValidColumn.Name = "ValidEmail"
            ValidColumn.ReadOnly = True
            dgvData.Columns.Add(ValidColumn)

            GetTableData = True
            Log("Table data successfully imported")
        Catch ex As Exception
            Log("Unable to import table data")
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            GetTableData = False
        End Try
    End Function

#End Region

#Region "Log data"

    Private Sub Log(StrData As String)
        txtLogs.AppendText(String.Format("- {0} {1} {2}", Now.ToString("HH:mm:ss"), _
                                         StrData, vbCrLf))
    End Sub

#End Region

#End Region

#Region "Check response"

    Private Class CheckResponse

        Private _RowIndex As Integer
        Public Property RowIndex() As Integer
            Get
                Return _RowIndex
            End Get
            Set(ByVal value As Integer)
                _RowIndex = value
            End Set
        End Property

        Private _IsValid As Boolean
        Public Property IsValid() As Boolean
            Get
                Return _IsValid
            End Get
            Set(ByVal value As Boolean)
                _IsValid = value
            End Set
        End Property

        Private _Response As String
        Public Property Response() As String
            Get
                Return _Response
            End Get
            Set(ByVal value As String)
                _Response = value
            End Set
        End Property

    End Class

#End Region

End Class