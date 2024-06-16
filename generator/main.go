package main

import (
	"bytes"
	"encoding/binary"
	"encoding/json"
  "encoding/base64"
  "github.com/buildkite/shellwords"
	"fmt"
	"io"
	"os"
	"strings"
	"unicode/utf16"
)


type CompileCommandEntry struct {
  Command   string `json:"command"`
  File      string `json:"file"`
  Directory string `json:"directory"`
}

type BaseConfigForBuild struct {
  Compiler  string    `json:"compiler"`
  Files     []string  `json:"files"`
}


func main() {
  fileName := os.Args[1]
  fileName = fileName[1:]

  // fileName := "C:\\tools\\letssee.txt"


  // os.WriteFile("C:\\tools\\testvalue.txt", []byte(err.Error()), os.ModePerm)
  jsonFileRead, err := os.Open(fileName)
  if err != nil {
    return
  }
  byteValue, _ := io.ReadAll(jsonFileRead)
  stringByteValue, err := DecodeUtf16(byteValue, binary.LittleEndian)
  if err != nil {
    fmt.Println("shit bro")
  }
  
  stringByteValue = strings.Replace(stringByteValue, "\ufeff", "", -1)

  strpath, err := os.Getwd()
  // strpath = "C:\\tools"
  var path_compile_location string = strings.Join([]string{strpath, "\\.compile_commands"}, "")

  config, err := GetContentForBuildConfig(path_compile_location)


  var command string = DeleteFiles(stringByteValue, config.Files)

  for _, file := range config.Files {
    entry := CompileCommandEntry{
      File: file,
      Command: strings.Join([]string{config.Compiler, command, file}, " "),
      // Command: command,
      Directory: strpath,
    }
    command_compile := base64.StdEncoding.EncodeToString([]byte(file))
    command_compile = strings.Join([]string{path_compile_location, "\\", command_compile, ".json"}, "")
    compileCommandsJson, _ := json.Marshal(entry)
    os.WriteFile(command_compile, compileCommandsJson, os.ModePerm)
  }
}

func ManageParsedCommandLine(strByteValue string, config *BaseConfigForBuild) []string {
  parsedList, err := shellwords.Split(strByteValue)
  if err != nil {
    return []string{}
  }
  var lists []string = []string{}
  for _, strValue := range parsedList {
    if !Contains(config.Files, strValue) {
      lists = append(lists, strValue)
    }
    lists = append(lists, strValue)
  }
  return lists
}

func GetContentForBuildConfig(path_compile_location string) (BaseConfigForBuild, error) {
  // try to read txt file here and not the json. the txt may be clear I think
  var compile_path_file string = strings.Join([]string{path_compile_location, "compiler_path.txt"}, "\\")
  var files_path_file string = strings.Join([]string{path_compile_location, "files_path.txt"}, "\\")
  var config BaseConfigForBuild;
  compile_path_file, err1 := ReadFile(compile_path_file)
  compile_files_content, err2 := ReadFile(files_path_file)
  if err1 != nil {
    return config, err1
  }
  if err2 != nil {
    return config, err2
  }
  var files_names []string = strings.Split(compile_files_content, "\n")
  config.Compiler = strings.Join([]string{"\"", compile_path_file, "\""}, "")
  config.Files = files_names
  return config, nil 
}

func ReadFile(file_location string) (result string, err error) {
  result = ""
  err = nil
  f, err := os.Open(file_location)
  if err != nil {
    return
  }
  fileByte, err := io.ReadAll(f)
  if err != nil {
    return
  }
  result = string(fileByte)
  return
}

func Contains(strList []string, strVal string) bool {
  for _, _str := range strList {
    if strVal == _str {
      return true
    }
  }
  return false
}

func GetFilePath(strValue string) string {
  result := 0
  for i := len(strValue) - 1; i >= 0; i-- {
    if (strValue[i] == '\\' || strValue[i] == '/') && result == 0  {
      result = i
    }
  }
  return strValue[:result]
}

func GetFileName(strValue string) string {
  end := false
  i := len(strValue) - 1
  var result []byte = []byte{}
  var endChar byte = '"'
  for !end {
    if len(result) <= 0 {
      if strValue[i] != byte(endChar) {
        endChar = ' '
      } else {
        i--
      }
    }
    if strValue[i] == endChar {
      end = true
    } else {
      result = append(result, strValue[i])
      i--
    }
  }
  for j, k := 0, len(result)-1; j < k; j, k = j+1, k-1 {
    result[j], result[k] = result[k], result[j]
  }
  return string(result)
}


func DecodeUtf16(b []byte, order binary.ByteOrder) (result string, err error) {
  ints := make([]uint16, len(b)/2)
  if err := binary.Read(bytes.NewReader(b), order, &ints); err != nil {
    return result, err
  }
  return string(utf16.Decode(ints)), nil
}

func DeleteFiles(command string, files []string) string {
  var toDelete int = 0
  for _, strVal := range files {
    var needQuote bool = strings.Contains(strVal, "\"") || strings.Contains(strVal, " ") || strings.Contains(strVal, "\\")
    if needQuote {
      toDelete += 2
    }
    toDelete += len(strVal)
    toDelete += 1
  }
  toDelete -= len(command)
  toDelete *= -1
  return command[:toDelete]
}
